using Cabinet.Core;

namespace Cabinet.Abstractions;

/// <summary>
/// Defines the contract for an offline data store that provides encrypted storage,
/// retrieval, and search capabilities for structured data.
/// </summary>
/// <remarks>
/// <para>
/// <strong>For most users:</strong> Use <see cref="RecordSet{T}"/> instead of this interface directly.
/// RecordSet provides a higher-level, domain-oriented API with automatic caching, CRUD operations,
/// and simpler file management.
/// </para>
/// <para>
/// <strong>Use IOfflineStore directly only if:</strong>
/// <list type="bullet">
/// <item>You need maximum control over storage behavior</item>
/// <item>You're implementing a custom storage provider</item>
/// <item>You have specialized requirements not covered by RecordSet</item>
/// </list>
/// </para>
/// <para>
/// See the documentation for architectural guidance on choosing between RecordSet and IOfflineStore.
/// </para>
/// </remarks>
public interface IOfflineStore
{
    /// <summary>
    /// Saves data with the specified identifier to encrypted storage.
    /// </summary>
    /// <typeparam name="T">The type of data to save</typeparam>
    /// <param name="id">The unique identifier for this record</param>
    /// <param name="data">The data to save</param>
    /// <param name="attachments">Optional file attachments to store with the record</param>
    /// <returns>A task representing the asynchronous save operation</returns>
    Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null);
    
    /// <summary>
    /// Loads data with the specified identifier from encrypted storage.
    /// </summary>
    /// <typeparam name="T">The type of data to load</typeparam>
    /// <param name="id">The unique identifier of the record to load</param>
    /// <returns>The loaded data, or null if the record does not exist</returns>
    Task<T?> LoadAsync<T>(string id);
    
    /// <summary>
    /// Deletes the record and all associated attachments with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the record to delete</param>
    /// <returns>A task representing the asynchronous delete operation</returns>
    Task DeleteAsync(string id);
    
    /// <summary>
    /// Searches for records matching the specified query string.
    /// </summary>
    /// <param name="query">The search query to match against indexed content</param>
    /// <returns>An enumerable collection of search results with metadata</returns>
    Task<IEnumerable<SearchResult>> FindAsync(string query);
    
    /// <summary>
    /// Searches for records matching the specified query string and returns typed results with data.
    /// </summary>
    /// <typeparam name="T">The type of data in the records</typeparam>
    /// <param name="query">The search query to match against indexed content</param>
    /// <returns>An enumerable collection of typed search results with data</returns>
    Task<IEnumerable<SearchResult<T>>> FindAsync<T>(string query);
}
