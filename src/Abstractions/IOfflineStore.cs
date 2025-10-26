using Plugin.Maui.OfflineData.Core;

namespace Plugin.Maui.OfflineData.Abstractions;

public interface IOfflineStore
{
    Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null);
    Task<T?> LoadAsync<T>(string id);
    Task DeleteAsync(string id);
    Task<IEnumerable<SearchResult>> SearchAsync(string query);
}
