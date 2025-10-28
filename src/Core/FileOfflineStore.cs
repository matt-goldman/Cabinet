using System.Text.Json;
using Plugin.Maui.OfflineData.Abstractions;

namespace Plugin.Maui.OfflineData.Core;

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
    /// Saves data with the specified identifier to encrypted storage.
    /// The data is serialized to JSON, encrypted, and written atomically to disk.
    /// </summary>
    /// <typeparam name="T">The type of data to save</typeparam>
    /// <param name="id">The unique identifier for this record</param>
    /// <param name="data">The data to save</param>
    /// <param name="attachments">Optional file attachments to store with the record</param>
    /// <returns>A task representing the asynchronous save operation</returns>
    public async Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
        var enc = await _crypto.EncryptAsync(json, id);
        var path = Path.Combine(_root, "records", $"{id}.dat.tmp");
        await File.WriteAllBytesAsync(path, enc);
        File.Move(path, path.Replace(".dat.tmp", ".dat"), true);

        if (_indexer != null)
            await _indexer.IndexAsync(id, JsonSerializer.Serialize(data, _jsonOptions), new Dictionary<string, string>());

        if (attachments != null)
        {
            foreach (var att in attachments)
            {
                var attPath = Path.Combine(_root, "attachments", $"{id}-{att.LogicalName}.bin.tmp");
                using var mem = new MemoryStream();
                await att.Content.CopyToAsync(mem);
                var encBytes = await _crypto.EncryptAsync(mem.ToArray(), id);
                await File.WriteAllBytesAsync(attPath, encBytes);
                File.Move(attPath, attPath.Replace(".bin.tmp", ".bin"), true);
            }
        }
    }

    /// <summary>
    /// Loads data with the specified identifier from encrypted storage.
    /// The encrypted file is read, decrypted, and deserialized from JSON.
    /// </summary>
    /// <typeparam name="T">The type of data to load</typeparam>
    /// <param name="id">The unique identifier of the record to load</param>
    /// <returns>The loaded data, or null if the record does not exist</returns>
    public async Task<T?> LoadAsync<T>(string id)
    {
        var path = Path.Combine(_root, "records", $"{id}.dat");
        if (!File.Exists(path)) return default;

        var enc = await File.ReadAllBytesAsync(path);
        var dec = await _crypto.DecryptAsync(enc, id);
        return JsonSerializer.Deserialize<T>(dec.AsSpan(), _jsonOptions);
    }

    /// <summary>
    /// Deletes the record and all associated attachments with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the record to delete</param>
    /// <returns>A task representing the asynchronous delete operation</returns>
    public Task DeleteAsync(string id)
    {
        var record = Path.Combine(_root, "records", $"{id}.dat");
        if (File.Exists(record)) File.Delete(record);

        foreach (var file in Directory.GetFiles(Path.Combine(_root, "attachments"), $"{id}-*.bin"))
            File.Delete(file);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Searches for records matching the specified query string.
    /// Requires an index provider to be configured.
    /// </summary>
    /// <param name="query">The search query to match against indexed content</param>
    /// <returns>An enumerable collection of search results with metadata, or an empty collection if no indexer is configured</returns>
    public async Task<IEnumerable<SearchResult>> FindAsync(string query)
        => _indexer != null
            ? await _indexer.QueryAsync(query)
            : [];

    /// <summary>
    /// Searches for records matching the specified query string and returns typed results with data.
    /// Attempts to load records as type T or as List&lt;T&gt; (for aggregate file patterns).
    /// Requires an index provider to be configured.
    /// </summary>
    /// <typeparam name="T">The type of data in the records</typeparam>
    /// <param name="query">The search query to match against indexed content</param>
    /// <returns>An enumerable collection of typed search results with data, or an empty collection if no indexer is configured</returns>
    public async Task<IEnumerable<SearchResult<T>>> FindAsync<T>(string query)
    {
        if (_indexer == null)
            return [];

        var results = await _indexer.QueryAsync(query);
        
        // Create tasks from the pure function
        var loadTasks = results.Select(LoadSearchResultAsync<T>).ToArray();

        var allResults = await Task.WhenAll(loadTasks).ConfigureAwait(false);
        var typedResults = new List<SearchResult<T>>();
        foreach (var resultList in allResults)
        {
            typedResults.AddRange(resultList);
        }
        return typedResults;
    }

    private async Task<List<SearchResult<T>>> LoadSearchResultAsync<T>(SearchResult result)
    {
        var typedResults = new List<SearchResult<T>>();
        
        // Try to load as T first
        try
        {
            var data = await LoadAsync<T>(result.RecordId).ConfigureAwait(false);
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
        catch (System.Text.Json.JsonException)
        {
            // If it fails, try as IList<T> (aggregate file pattern)
        }

        // Try to load as IList<T> (for aggregate file pattern)
        try
        {
            var listData = await LoadAsync<List<T>>(result.RecordId).ConfigureAwait(false);
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
        catch (System.Text.Json.JsonException)
        {
            // Neither T nor List<T> worked, skip this result
        }
        
        return typedResults;
    }

}
