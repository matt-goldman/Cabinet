using Cabinet.Core;

namespace Cabinet.Abstractions;

/// <summary>
/// Defines the contract for an index provider that enables full-text search
/// and metadata-based querying of stored records.
/// </summary>
public interface IIndexProvider
{
    /// <summary>
    /// Adds or updates a record in the search index.
    /// </summary>
    /// <param name="id">The unique identifier of the record</param>
    /// <param name="content">The searchable text content to index</param>
    /// <param name="metadata">Additional metadata to store with the indexed record</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>A task representing the asynchronous indexing operation</returns>
    Task IndexAsync(string id, string content, IDictionary<string, string> metadata, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Queries the index for records matching the specified search terms.
    /// </summary>
    /// <param name="query">The search query containing one or more terms</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>An enumerable collection of matching search results ordered by relevance score</returns>
    Task<IEnumerable<SearchResult>> QueryAsync(string query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clears all entries from the index.
    /// </summary>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>A task representing the asynchronous clear operation</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
