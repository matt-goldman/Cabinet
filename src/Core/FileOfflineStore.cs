using System.Text.Json;
using Cabinet.Abstractions;

namespace Cabinet.Core;

/// <summary>
/// A file-based implementation of <see cref="IOfflineStore"/> that stores encrypted records
/// and attachments in the local file system with optional full-text search indexing.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Attachment layout.</strong> Each record's attachments live in their own directory, keyed
/// by a hash of the record id, alongside an encrypted manifest describing them:
/// </para>
/// <code>
/// attachments/{hash(recordId)}/manifest.dat   // encrypted AttachmentInfo list
/// attachments/{hash(recordId)}/{hash(name)}.bin
/// </code>
/// <para>
/// Names are hashed rather than used directly, so an arbitrary logical name cannot escape the
/// attachments directory, and one record's attachments cannot be confused with another's.
/// </para>
/// <para>
/// <strong>Commit ordering.</strong> A save stages every file with a <c>.tmp</c> suffix, then renames
/// the attachment blobs, then the manifest, then the record. The record rename is the commit point:
/// if a save fails or the process dies partway through, any files already written are unreferenced by
/// a record and are swept on the next save. A record never becomes visible referencing attachments
/// that are not on disk.
/// </para>
/// <para>
/// This is crash consistency, not durability. The staged writes are not flushed to the storage medium
/// before being renamed, so a power loss (as opposed to a process crash) can still leave a renamed
/// file with incomplete contents. Cabinet is not a database and does not offer a durability guarantee.
/// </para>
/// </remarks>
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
        CreateDirectories();
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
        CreateDirectories();
    }

    private void CreateDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "records"));
        Directory.CreateDirectory(Path.Combine(_root, "attachments"));
        Directory.CreateDirectory(Path.Combine(_root, "index"));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The data is serialised to JSON, encrypted, and written atomically to disk.
    /// If an index provider is configured, the content is automatically indexed.
    /// See the remarks on <see cref="FileOfflineStore"/> for the commit ordering used when
    /// attachments are supplied.
    /// </remarks>
    public async Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var recordPath = RecordPath(id);

        // Files written but not yet committed. On any failure these are removed, leaving the store
        // exactly as it was.
        var temps = new List<string>();

        // (temporary, final) pairs in commit order: attachment blobs, then the manifest, then the
        // record. The record rename is what makes the whole save visible.
        var commits = new List<(string Temp, string Final)>();

        try
        {
            if (attachments is not null)
            {
                var directory = AttachmentDirectory(id);
                Directory.CreateDirectory(directory);

                var manifest = new List<AttachmentInfo>();
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (var attachment in attachments)
                {
                    if (!seen.Add(attachment.LogicalName))
                    {
                        throw new ArgumentException(
                            $"Duplicate attachment name '{attachment.LogicalName}' for record '{id}'. " +
                            "Attachment names must be unique within a record.",
                            nameof(attachments));
                    }

                    var content = await ReadAllAsync(attachment.Content, cancellationToken).ConfigureAwait(false);
                    var encrypted = await _crypto.EncryptAsync(content, AttachmentContext(id, attachment.LogicalName), cancellationToken).ConfigureAwait(false);

                    var blobPath = BlobPath(directory, attachment.LogicalName);
                    var blobTemp = blobPath + ".tmp";

                    temps.Add(blobTemp);
                    await File.WriteAllBytesAsync(blobTemp, encrypted, cancellationToken).ConfigureAwait(false);
                    commits.Add((blobTemp, blobPath));

                    manifest.Add(new AttachmentInfo(attachment.LogicalName, attachment.ContentType, content.Length));
                }

                var manifestPath = ManifestPath(directory);
                var manifestTemp = manifestPath + ".tmp";

                temps.Add(manifestTemp);
                await WriteManifestAsync(manifestTemp, id, manifest, cancellationToken).ConfigureAwait(false);
                commits.Add((manifestTemp, manifestPath));
            }

            var recordTemp = recordPath + ".tmp";
            temps.Add(recordTemp);
            var json = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
            var encryptedRecord = await _crypto.EncryptAsync(json, id, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(recordTemp, encryptedRecord, cancellationToken).ConfigureAwait(false);
            commits.Add((recordTemp, recordPath));
        }
        catch
        {
            DeleteQuietly(temps);
            throw;
        }

        foreach (var (temp, final) in commits)
            File.Move(temp, final, true);

        // Past the commit point. Anything below is housekeeping and must not fail the save.
        if (attachments is not null)
            PruneAttachments(id, commits);

        if (_indexer != null)
            await _indexer.IndexAsync(id, JsonSerializer.Serialize(data, _jsonOptions), new Dictionary<string, string>(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The encrypted file is read, decrypted, and deserialised from JSON.
    /// </remarks>
    public async Task<T?> LoadAsync<T>(string id, CancellationToken cancellationToken = default)
    {
        var path = RecordPath(id);
        if (!File.Exists(path)) return default;

        var enc = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var dec = await _crypto.DecryptAsync(enc, id, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(dec.AsSpan(), _jsonOptions);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var record = RecordPath(id);
        if (File.Exists(record)) File.Delete(record);

        var directory = AttachmentDirectory(id);
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<AttachmentInfo> SaveAttachmentAsync(string id, FileAttachment attachment, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(attachment);

        var directory = AttachmentDirectory(id);
        Directory.CreateDirectory(directory);

        var content = await ReadAllAsync(attachment.Content, cancellationToken).ConfigureAwait(false);
        var encrypted = await _crypto.EncryptAsync(content, AttachmentContext(id, attachment.LogicalName), cancellationToken).ConfigureAwait(false);

        var info = new AttachmentInfo(attachment.LogicalName, attachment.ContentType, content.Length);

        var manifest = await ReadManifestAsync(id, cancellationToken).ConfigureAwait(false);
        manifest.RemoveAll(a => string.Equals(a.Name, attachment.LogicalName, StringComparison.Ordinal));
        manifest.Add(info);

        var blobPath = BlobPath(directory, attachment.LogicalName);
        var blobTemp = blobPath + ".tmp";
        var manifestPath = ManifestPath(directory);
        var manifestTemp = manifestPath + ".tmp";

        try
        {
            await File.WriteAllBytesAsync(blobTemp, encrypted, cancellationToken).ConfigureAwait(false);
            await WriteManifestAsync(manifestTemp, id, manifest, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DeleteQuietly([blobTemp, manifestTemp]);
            throw;
        }

        // Blob first, then the manifest: the manifest rename is what publishes the attachment.
        File.Move(blobTemp, blobPath, true);
        File.Move(manifestTemp, manifestPath, true);

        return info;
    }

    /// <inheritdoc/>
    public async Task<Stream?> OpenAttachmentAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        AttachmentName.Validate(name, nameof(name));

        var path = BlobPath(AttachmentDirectory(id), name);
        if (!File.Exists(path)) return null;

        var encrypted = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var decrypted = await _crypto.DecryptAsync(encrypted, AttachmentContext(id, name), cancellationToken).ConfigureAwait(false);

        return new MemoryStream(decrypted, writable: false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AttachmentInfo>> ListAttachmentsAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return await ReadManifestAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAttachmentAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        AttachmentName.Validate(name, nameof(name));

        var directory = AttachmentDirectory(id);
        var manifest = await ReadManifestAsync(id, cancellationToken).ConfigureAwait(false);

        if (manifest.RemoveAll(a => string.Equals(a.Name, name, StringComparison.Ordinal)) == 0)
            return false;

        var manifestPath = ManifestPath(directory);
        var manifestTemp = manifestPath + ".tmp";

        try
        {
            await WriteManifestAsync(manifestTemp, id, manifest, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            DeleteQuietly([manifestTemp]);
            throw;
        }

        // Publish the removal first, then reclaim the bytes. A crash in between leaves an orphan
        // blob, which the next save sweeps; the reverse order would leave a manifest entry
        // pointing at nothing.
        File.Move(manifestTemp, manifestPath, true);

        var blobPath = BlobPath(directory, name);
        if (File.Exists(blobPath)) File.Delete(blobPath);

        return true;
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

    private string RecordPath(string id) => Path.Combine(_root, "records", $"{id}.dat");

    private string AttachmentDirectory(string id) => Path.Combine(_root, "attachments", AttachmentName.ToFileName(id));

    private static string ManifestPath(string directory) => Path.Combine(directory, "manifest.dat");

    private static string BlobPath(string directory, string logicalName)
        => Path.Combine(directory, $"{AttachmentName.ToFileName(logicalName)}.bin");

    /// <summary>
    /// The additional authenticated data for an attachment blob. Binding both the record id and the
    /// attachment name means a blob cannot be substituted for another attachment, or for the same
    /// attachment on a different record, without failing authentication.
    /// </summary>
    /// <remarks>
    /// The separator is a NUL, which <see cref="AttachmentName.Validate"/> forbids in a name, so the
    /// pairing cannot be made ambiguous by a crafted name.
    /// </remarks>
    private static string AttachmentContext(string id, string logicalName) => $"{id}\u0000{logicalName}";

    private static string ManifestContext(string id) => $"{id}\u0000#manifest";

    private async Task WriteManifestAsync(string path, string id, List<AttachmentInfo> manifest, CancellationToken cancellationToken)
    {
        // Serialised with Cabinet's own context rather than _jsonOptions: a caller's source-generated
        // context knows only about their record types and would fail on AttachmentInfo under AOT.
        var json = JsonSerializer.SerializeToUtf8Bytes(manifest, AttachmentManifestJsonContext.Default.ListAttachmentInfo);
        var encrypted = await _crypto.EncryptAsync(json, ManifestContext(id), cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(path, encrypted, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<AttachmentInfo>> ReadManifestAsync(string id, CancellationToken cancellationToken)
    {
        var path = ManifestPath(AttachmentDirectory(id));
        if (!File.Exists(path)) return [];

        var encrypted = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var decrypted = await _crypto.DecryptAsync(encrypted, ManifestContext(id), cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize(decrypted.AsSpan(), AttachmentManifestJsonContext.Default.ListAttachmentInfo) ?? [];
    }

    private static async Task<byte[]> ReadAllAsync(Stream content, CancellationToken cancellationToken)
    {
        // Copied rather than read via Length: attachment streams are frequently non-seekable
        // (Android asset streams, network responses), where Length and Position both throw.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// Removes attachment files in the record's directory that the just-committed manifest does not
    /// reference: blobs left by a previous save, and temporary files left by an interrupted one.
    /// </summary>
    private void PruneAttachments(string id, List<(string Temp, string Final)> commits)
    {
        var directory = AttachmentDirectory(id);
        if (!Directory.Exists(directory)) return;

        var keep = commits
            .Select(c => Path.GetFileName(c.Final))
            .ToHashSet(StringComparer.Ordinal);

        keep.Add("manifest.dat");

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (!keep.Contains(Path.GetFileName(file)))
                    File.Delete(file);
            }
        }
        catch (IOException)
        {
            // Housekeeping only; the save has already been committed.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void DeleteQuietly(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
