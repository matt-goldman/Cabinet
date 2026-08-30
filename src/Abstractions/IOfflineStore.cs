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
    /// Saves data with the specified identifier to encrypted storage, together with any attachments.
    /// </summary>
    /// <typeparam name="T">The type of data to save</typeparam>
    /// <param name="id">The unique identifier for this record</param>
    /// <param name="data">The data to save</param>
    /// <param name="attachments">
    /// The complete set of attachments for this record, or <c>null</c> to leave any existing
    /// attachments untouched. Passing a non-null collection replaces the record's attachments, so an
    /// empty collection removes all of them.
    /// </param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>A task representing the asynchronous save operation</returns>
    /// <remarks>
    /// The record and its attachments are committed together: if any part of the write fails, no part
    /// of it becomes visible. Note that this is crash consistency, not durability — see the
    /// implementation's remarks for the limits of the guarantee.
    /// </remarks>
    Task SaveAsync<T>(string id, T data, IEnumerable<FileAttachment>? attachments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or replaces a single attachment on an existing record, leaving its other attachments
    /// and the record itself unchanged.
    /// </summary>
    /// <param name="id">The unique identifier of the record to attach to</param>
    /// <param name="attachment">The attachment to store</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>The metadata describing the stored attachment</returns>
    /// <remarks>
    /// An existing attachment with the same <see cref="FileAttachment.LogicalName"/> is replaced.
    /// </remarks>
    Task<AttachmentInfo> SaveAttachmentAsync(string id, FileAttachment attachment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the content of a single attachment for reading.
    /// </summary>
    /// <param name="id">The unique identifier of the record the attachment belongs to</param>
    /// <param name="name">The logical name of the attachment</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>A readable stream of the decrypted content, or null if no such attachment exists</returns>
    /// <remarks>The caller owns the returned stream and is responsible for disposing it.</remarks>
    Task<Stream?> OpenAttachmentAsync(string id, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the attachments stored against a record.
    /// </summary>
    /// <param name="id">The unique identifier of the record</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>The attachment metadata, or an empty list if the record has no attachments</returns>
    Task<IReadOnlyList<AttachmentInfo>> ListAttachmentsAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a single attachment from a record, leaving the record itself unchanged.
    /// </summary>
    /// <param name="id">The unique identifier of the record the attachment belongs to</param>
    /// <param name="name">The logical name of the attachment to delete</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>True if the attachment existed and was deleted, false otherwise</returns>
    Task<bool> DeleteAttachmentAsync(string id, string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Loads data with the specified identifier from encrypted storage.
    /// </summary>
    /// <typeparam name="T">The type of data to load</typeparam>
    /// <param name="id">The unique identifier of the record to load</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>The loaded data, or null if the record does not exist</returns>
    Task<T?> LoadAsync<T>(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes the record and all associated attachments with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the record to delete</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>A task representing the asynchronous delete operation</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for records matching the specified query string.
    /// </summary>
    /// <param name="query">The search query to match against indexed content</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>An enumerable collection of search results with metadata</returns>
    Task<IEnumerable<SearchResult>> FindAsync(string query, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Searches for records matching the specified query string and returns typed results with data.
    /// </summary>
    /// <typeparam name="T">The type of data in the records</typeparam>
    /// <param name="query">The search query to match against indexed content</param>
    /// <param name="cancellationToken">Optional token to cancel the operation</param>
    /// <returns>An enumerable collection of typed search results with data</returns>
    Task<IEnumerable<SearchResult<T>>> FindAsync<T>(string query, CancellationToken cancellationToken = default);
}
