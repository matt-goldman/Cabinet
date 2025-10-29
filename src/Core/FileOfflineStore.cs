using System.Text.Json;
using Cabinet.Abstractions;

namespace Cabinet.Core;

/// <summary>
/// A file-based implementation of <see cref="IOfflineStore"/> that stores encrypted records
/// and attachments in the local file system with optional full-text search indexing.
/// </summary>
public sealed class FileOfflineStore : IOfflineStore
{
    private readonly string _root;
    private readonly IEncryptionProvider _crypto;
    private readonly IIndexProvider? _indexer;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Initialises a new instance of the <see cref="FileOfflineStore"/> class.
    /// </summary>
    /// <param name="rootPath">The root directory path where records and attachments will be stored</param>
    /// <param name="crypto">The encryption provider to use for encrypting and decrypting data</param>
    /// <param name="indexer">Optional index provider for enabling search capabilities</param>
    public FileOfflineStore(string rootPath, IEncryptionProvider crypto, IIndexProvider? indexer = null)
    {
        _root = rootPath;
        _crypto = crypto;
        _indexer = indexer;
        Directory.CreateDirectory(Path.Combine(_root, "records"));
        Directory.CreateDirectory(Path.Combine(_root, "attachments"));
        Directory.CreateDirectory(Path.Combine(_root, "index"));
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="FileOfflineStore"/> class with custom JSON serialisation options.
    /// This constructor is designed for AOT scenarios where JSON source generation is required.
    /// </summary>
    /// <param name="rootPath">The root directory path where records and attachments will be stored</param>
    /// <param name="crypto">The encryption provider to use for encrypting and decrypting data</param>
    /// <param name="jsonOptions">Custom JSON serialiser options for AOT-compatible serialisation</param>
    /// <param name="indexer">Optional index provider for enabling search capabilities</param>
    public FileOfflineStore(string rootPath, IEncryptionProvider crypto, JsonSerializerOptions jsonOptions, IIndexProvider? indexer = null)
    {
        _root = rootPath;
        _crypto = crypto;
        _jsonOptions = jsonOptions;
        _indexer = indexer;
        Directory.CreateDirectory(Path.Combine(_root, "records"));
        Directory.CreateDirectory(Path.Combine(_root, "attachments"));
        Directory.CreateDirectory(Path.Combine(_root, "index"));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The data is serialised to JSON, encrypted, and written atomically to disk.
    /// If an index provider is configured, the content is automatically indexed.
    /// </remarks>
    public async Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
        var enc = await _crypto.EncryptAsync(json, id, cancellationToken);
        var path = Path.Combine(_root, "records", $"{id}.dat.tmp");
        await File.WriteAllBytesAsync(path, enc, cancellationToken);
        File.Move(path, path.Replace(".dat.tmp", ".dat"), true);

        if (_indexer != null)
            await _indexer.IndexAsync(id, JsonSerializer.Serialize(data, _jsonOptions), new Dictionary<string, string>(), cancellationToken);

        if (attachments != null)
        {
            foreach (var att in attachments)
            {
                var attPath = Path.Combine(_root, "attachments", $"{id}-{att.LogicalName}.bin.tmp");
                using var mem = new MemoryStream();
                await att.Content.CopyToAsync(mem, cancellationToken);
                var encBytes = await _crypto.EncryptAsync(mem.ToArray(), id, cancellationToken);
                await File.WriteAllBytesAsync(attPath, encBytes, cancellationToken);
                File.Move(attPath, attPath.Replace(".bin.tmp", ".bin"), true);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The encrypted file is read, decrypted, and deserialised from JSON.
    /// </remarks>
    public async Task<T?> LoadAsync<T>(string id, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_root, "records", $"{id}.dat");
        if (!File.Exists(path)) return default;

        var enc = await File.ReadAllBytesAsync(path, cancellationToken);
        var dec = await _crypto.DecryptAsync(enc, id, cancellationToken);
        return JsonSerializer.Deserialize<T>(dec.AsSpan(), _jsonOptions);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var record = Path.Combine(_root, "records", $"{id}.dat");
        if (File.Exists(record)) File.Delete(record);

        foreach (var file in Directory.GetFiles(Path.Combine(_root, "attachments"), $"{id}-*.bin"))
            File.Delete(file);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Requires an index provider to be configured.
    /// </remarks>
    public async Task<IEnumerable<SearchResult>> FindAsync(string query, CancellationToken cancellationToken = default)
        => _indexer != null
            ? await _indexer.QueryAsync(query, cancellationToken)
            : [];

    /// <inheritdoc/>
    /// <remarks>
    /// Attempts to load records as type T or as List&lt;T&gt; (for aggregate file patterns).
    /// Requires an index provider to be configured.
    /// </remarks>
    public async Task<IEnumerable<SearchResult<T>>> FindAsync<T>(string query, CancellationToken cancellationToken = default)
    {
        if (_indexer == null)
            return [];

        var results = await _indexer.QueryAsync(query, cancellationToken);
        
        // Create tasks from the pure function
        var loadTasks = results.Select(r => LoadSearchResultAsync<T>(r, cancellationToken)).ToArray();

        var allResults = await Task.WhenAll(loadTasks).ConfigureAwait(false);
        var typedResults = new List<SearchResult<T>>();
        foreach (var resultList in allResults)
        {
            typedResults.AddRange(resultList);
        }
        return typedResults;
    }

    private async Task<List<SearchResult<T>>> LoadSearchResultAsync<T>(SearchResult result, CancellationToken cancellationToken = default)
    {
        var typedResults = new List<SearchResult<T>>();
        
        // Try to load as T first
        try
        {
            var data = await LoadAsync<T>(result.RecordId, cancellationToken).ConfigureAwait(false);
            if (data != null)
            {
                typedResults.Add(new SearchResult<T>(
                    result.RecordId,
                    result.Score,
                    result.Header,
                    data));
                return typedResults;
            }
        }
        catch (JsonException)
        {
            // If it fails, try as IList<T> (aggregate file pattern)
        }

        // Try to load as IList<T> (for aggregate file pattern)
        try
        {
            var listData = await LoadAsync<List<T>>(result.RecordId, cancellationToken).ConfigureAwait(false);
            if (listData != null && listData.Count > 0)
            {
                // Add each item in the list as a separate result
                foreach (var item in listData)
                {
                    typedResults.Add(new SearchResult<T>(
                        result.RecordId,
                        result.Score,
                        result.Header,
                        item));
                }
            }
        }
        catch (JsonException)
        {
            // Neither T nor List<T> worked, skip this result
        }
        
        return typedResults;
    }

}
