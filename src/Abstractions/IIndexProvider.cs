using Plugin.Maui.OfflineData.Core;

namespace Plugin.Maui.OfflineData.Abstractions;

public interface IIndexProvider
{
    Task IndexAsync(string id, string content, IDictionary<string, string> metadata);
    Task<IEnumerable<SearchResult>> QueryAsync(string query);
    Task ClearAsync();
}
