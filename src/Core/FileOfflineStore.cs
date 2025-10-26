using System.Text.Json;
using Plugin.Maui.OfflineData.Abstractions;

namespace Plugin.Maui.OfflineData.Core;

public sealed class FileOfflineStore : IOfflineStore
{
    private readonly string _root;
    private readonly IEncryptionProvider _crypto;
    private readonly IIndexProvider? _indexer;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = false };

    public FileOfflineStore(string rootPath, IEncryptionProvider crypto, IIndexProvider? indexer = null)
    {
        _root = rootPath;
        _crypto = crypto;
        _indexer = indexer;
        Directory.CreateDirectory(Path.Combine(_root, "records"));
        Directory.CreateDirectory(Path.Combine(_root, "attachments"));
        Directory.CreateDirectory(Path.Combine(_root, "index"));
    }

    public async Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
        var enc = await _crypto.EncryptAsync(json, id);
        var path = Path.Combine(_root, "records", $"{id}.dat.tmp");
        await File.WriteAllBytesAsync(path, enc);
        File.Move(path, Path.ChangeExtension(path, ".dat"), true);

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
                File.Move(attPath, Path.ChangeExtension(attPath, ".bin"), true);
            }
        }
    }

    public async Task<T?> LoadAsync<T>(string id)
    {
        var path = Path.Combine(_root, "records", $"{id}.dat");
        if (!File.Exists(path)) return default;

        var enc = await File.ReadAllBytesAsync(path);
        var dec = await _crypto.DecryptAsync(enc, id);
        return JsonSerializer.Deserialize<T>(dec.AsSpan(), _jsonOptions);
    }

    public Task DeleteAsync(string id)
    {
        var record = Path.Combine(_root, "records", $"{id}.dat");
        if (File.Exists(record)) File.Delete(record);

        foreach (var file in Directory.GetFiles(Path.Combine(_root, "attachments"), $"{id}-*.bin"))
            File.Delete(file);

        return Task.CompletedTask;
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(string query)
        => _indexer != null
            ? await _indexer.QueryAsync(query)
            : [];


}
