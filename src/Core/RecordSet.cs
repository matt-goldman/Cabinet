using Cabinet.Abstractions;
using System.Reflection;

namespace Cabinet.Core;

/// <summary>
/// Provides a high-level, domain-oriented abstraction for working with a collection of records of type T.
/// RecordSet handles file discovery, loading, caching, indexing, and CRUD operations automatically.
/// </summary>
/// <typeparam name="T">The type of records in this record set</typeparam>
/// <remarks>
/// <para>
/// <strong>This is the recommended API for most Cabinet users.</strong>
/// RecordSet provides sensible defaults that "just work" for datasets under ~10,000 records (~100MB).
/// All records are cached in memory after first load for fast queries.
/// </para>
/// <para>
/// <strong>Features:</strong>
/// <list type="bullet">
/// <item>Automatic file name discovery (defaults to TypeName.dat)</item>
/// <item>Auto-discovery of ID properties (Id or {TypeName}Id)</item>
/// <item>In-memory caching for fast queries</item>
/// <item>CRUD operations with automatic persistence</item>
/// <item>LINQ-style querying (Where, OrderBy, etc.)</item>
/// <item>Full-text search via encrypted indexes</item>
/// </list>
/// </para>
/// <para>
/// For AOT compatibility, provide an <c>IdSelector</c> in <see cref="RecordSetOptions{T}"/>
/// to avoid reflection-based ID discovery.
/// </para>
/// <para>
/// For advanced scenarios requiring custom storage behavior or memory management,
/// use <see cref="IOfflineStore"/> directly.
/// </para>
/// </remarks>
public sealed class RecordSet<T> where T : class
{
	private readonly IOfflineStore _store;
	private readonly RecordSetOptions<T> _options;
	private readonly string _fileName;
	private readonly Func<T, string> _idGetter;
	private Dictionary<string, T>? _cache;
	private Dictionary<string, IReadOnlyList<AttachmentInfo>>? _attachmentCache;
	private bool _isLoaded;

	/// <summary>
	/// Creates a new RecordSet for the specified type.
	/// </summary>
	/// <param name="store">The offline store to use</param>
	/// <param name="options">Optional configuration (uses sensible defaults if null)</param>
	/// <remarks>
	/// For AOT compatibility, provide an IdSelector in options:
	/// <code>
	/// var options = new RecordSetOptions&lt;LessonRecord&gt;
	/// {
	///     IdSelector = lesson => lesson.LessonId
	/// };
	/// var lessons = new RecordSet&lt;LessonRecord&gt;(store, options);
	/// </code>
	/// </remarks>
	public RecordSet(IOfflineStore store, RecordSetOptions<T>? options = null)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_options = options ?? new RecordSetOptions<T>();

		// Determine file name (default: TypeName.dat will be added by store)
		_fileName = _options.CustomFileName ?? typeof(T).Name;

		// Set up ID getter (prefer IdSelector for AOT safety)
		_idGetter = _options.IdSelector ?? CreateReflectionBasedIdGetter();
	}

	/// <summary>
	/// Loads all records of this type into memory cache.
	/// This is typically called once at startup or when you need to refresh from disk.
	/// </summary>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	public async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		var records = await _store.LoadAsync<List<T>>(_fileName, cancellationToken);
		_attachmentCache = null;

		if (records == null)
		{
			_cache = [];
			_isLoaded = true;
			return;
		}

		if (_options.EnableCaching)
		{
			_cache = records.ToDictionary(GetId);
		}

		_isLoaded = true;
	}

	/// <summary>
	/// Gets all records in the record set. Loads from disk if not already loaded.
	/// </summary>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>All records of type T</returns>
	public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await EnsureLoadedAsync(cancellationToken);
		return _cache?.Values.ToList() ?? [];
	}

	/// <summary>
	/// Gets a record by its ID. Loads from disk if not already loaded.
	/// </summary>
	/// <param name="id">The ID of the record to retrieve</param>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>The record if found, null otherwise</returns>
	public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
	{
		await EnsureLoadedAsync(cancellationToken);
		return _cache?.GetValueOrDefault(id);
	}

	/// <summary>
	/// Adds a new record to the record set. Automatically persists to disk.
	/// </summary>
	/// <param name="record">The record to add</param>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	public async Task AddAsync(T record, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(record);

		await EnsureLoadedAsync(cancellationToken);

		var id = GetId(record);

		// Add to cache
		if (_cache != null)
		{
			_cache[id] = record;
		}

		// Persist to disk
		await SaveAllAsync(cancellationToken);
	}

	/// <summary>
	/// Updates an existing record. Automatically persists to disk.
	/// </summary>
	/// <param name="id">The ID of the record to update</param>
	/// <param name="record">The updated record</param>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>True if the record was found and updated, false otherwise</returns>
	public async Task<bool> UpdateAsync(string id, T record, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(record);

		await EnsureLoadedAsync(cancellationToken);

		if (_cache == null || !_cache.ContainsKey(id))
		{
			return false;
		}

		_cache[id] = record;
		await SaveAllAsync(cancellationToken);
		return true;
	}

	/// <summary>
	/// Removes a record by its ID, along with any attachments held against it.
	/// Automatically persists to disk.
	/// </summary>
	/// <param name="id">The ID of the record to remove</param>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>True if the record was found and removed, false otherwise</returns>
	/// <remarks>
	/// The set is saved before the attachments are deleted, so an interruption between the two
	/// leaves orphaned attachment bytes rather than a record referencing attachments that are gone.
	/// Orphans can be reclaimed with <see cref="CompactAttachmentsAsync"/>.
	/// </remarks>
	public async Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
	{
		await EnsureLoadedAsync(cancellationToken);

		if (_cache == null || !_cache.Remove(id))
		{
			return false;
		}

		await SaveAllAsync(cancellationToken);

		// Publish the removal first, then reclaim the bytes. DeleteAsync on the attachment key removes
		// the whole attachment directory; no record is ever stored under that key, so nothing else goes
		// with it.
		await _store.DeleteAsync(AttachmentKey(id), cancellationToken);
		_attachmentCache?.Remove(id);

		return true;
	}

	/// <summary>
	/// Adds or replaces an attachment on a record in this set. The bytes are stored as a separate
	/// encrypted file; the record itself is not rewritten.
	/// </summary>
	/// <param name="recordId">The ID of the record to attach to</param>
	/// <param name="attachment">The attachment to store</param>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>The metadata describing the stored attachment</returns>
	/// <remarks>
	/// Keep the returned <see cref="AttachmentInfo"/> on your record if you want the attachment
	/// discoverable from the record itself, and save the record as usual. An existing attachment with
	/// the same name is replaced.
	/// </remarks>
	public async Task<AttachmentInfo> AddAttachmentAsync(string recordId, FileAttachment attachment, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(recordId);
		ArgumentNullException.ThrowIfNull(attachment);

		var info = await _store.SaveAttachmentAsync(AttachmentKey(recordId), attachment, cancellationToken);
		_attachmentCache?.Remove(recordId);

		return info;
	}

	/// <summary>
	/// Opens the content of an attachment on a record in this set.
	/// </summary>
	/// <param name="recordId">The ID of the record the attachment belongs to</param>
	/// <param name="name">The logical name of the attachment</param>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>A readable stream of the decrypted content, or null if no such attachment exists</returns>
	/// <remarks>The caller owns the returned stream and is responsible for disposing it.</remarks>
	public Task<Stream?> OpenAttachmentAsync(string recordId, string name, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(recordId);
		return _store.OpenAttachmentAsync(AttachmentKey(recordId), name, cancellationToken);
	}

	/// <summary>
	/// Lists the attachments held against a record in this set.
	/// </summary>
	/// <param name="recordId">The ID of the record</param>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>The attachment metadata, or an empty list if the record has no attachments</returns>
	/// <remarks>
	/// Metadata is cached in memory when caching is enabled; the content itself is never cached.
	/// </remarks>
	public async Task<IReadOnlyList<AttachmentInfo>> ListAttachmentsAsync(string recordId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(recordId);

		if (_attachmentCache != null && _attachmentCache.TryGetValue(recordId, out var cached))
		{
			return cached;
		}

		var attachments = await _store.ListAttachmentsAsync(AttachmentKey(recordId), cancellationToken);

		if (_options.EnableCaching)
		{
			_attachmentCache ??= [];
			_attachmentCache[recordId] = attachments;
		}

		return attachments;
	}

	/// <summary>
	/// Removes a single attachment from a record in this set.
	/// </summary>
	/// <param name="recordId">The ID of the record the attachment belongs to</param>
	/// <param name="name">The logical name of the attachment to remove</param>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>True if the attachment existed and was removed, false otherwise</returns>
	public async Task<bool> RemoveAttachmentAsync(string recordId, string name, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrEmpty(recordId);

		var removed = await _store.DeleteAttachmentAsync(AttachmentKey(recordId), name, cancellationToken);
		_attachmentCache?.Remove(recordId);

		return removed;
	}

	/// <summary>
	/// Deletes attachments belonging to records that are no longer in this set.
	/// </summary>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <returns>The number of records whose orphaned attachments were removed</returns>
	/// <remarks>
	/// Attachments are normally removed with their record by <see cref="RemoveAsync"/>. This reclaims
	/// what an interrupted removal left behind, and is safe but not cheap: every attachment directory
	/// in the store is opened. Call it on a maintenance path, not on every load.
	/// </remarks>
	public async Task<int> CompactAttachmentsAsync(CancellationToken cancellationToken = default)
	{
		await EnsureLoadedAsync(cancellationToken);

		var prefix = $"{_fileName}#";
		var owners = await _store.ListAttachmentRecordIdsAsync(cancellationToken);
		var removed = 0;

		foreach (var owner in owners)
		{
			// Only this set's attachments; another RecordSet's records are not ours to judge.
			if (!owner.StartsWith(prefix, StringComparison.Ordinal))
			{
				continue;
			}

			var recordId = owner[prefix.Length..];
			if (_cache != null && _cache.ContainsKey(recordId))
			{
				continue;
			}

			await _store.DeleteAsync(owner, cancellationToken);
			_attachmentCache?.Remove(recordId);
			removed++;
		}

		return removed;
	}

	/// <summary>
	/// Searches records using the encrypted index.
	/// </summary>
	/// <param name="terms">Search terms to match</param>
	/// <returns>Records matching the search terms</returns>
	public async Task<IEnumerable<T>> FindAsync(params string[] terms)
	{
		return await FindAsync(CancellationToken.None, terms);
	}

	/// <summary>
	/// Searches records using the encrypted index.
	/// </summary>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	/// <param name="terms">Search terms to match</param>
	/// <returns>Records matching the search terms</returns>
	public async Task<IEnumerable<T>> FindAsync(CancellationToken cancellationToken, params string[] terms)
	{
		if (terms.Length == 0)
		{
			return await GetAllAsync(cancellationToken);
		}

		var query = string.Join(" ", terms);
		var results = await _store.FindAsync<T>(query, cancellationToken);

		// Filter to only records from this type's file
		return results
		.Where(r => r.RecordId == _fileName)
		.Select(r => r.Data);
	}

	/// <summary>
	/// Filters records using a predicate. Operates on cached data.
	/// </summary>
	/// <param name="predicate">The filter predicate</param>
	/// <returns>Matching records</returns>
	public IEnumerable<T> Where(Func<T, bool> predicate)
	{
		EnsureLoaded();
		return _cache?.Values.Where(predicate) ?? Enumerable.Empty<T>();
	}

	/// <summary>
	/// Gets all loaded records as queryable.
	/// </summary>
	/// <returns>Queryable records from the cache</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the record set has not been loaded, or when caching is disabled and no in-memory cache is available.
	/// Call LoadAsync() or GetAllAsync() first, and ensure caching is enabled.
	/// </exception>
	public IQueryable<T> AsQueryable()
	{
		EnsureLoaded();
		if (_cache == null)
		{
			throw new InvalidOperationException(
				$"Queryable access for RecordSet<{typeof(T).Name}> requires an in-memory cache. Ensure caching is enabled before calling AsQueryable().");
		}

		return _cache.Values.AsQueryable();
	}

	/// <summary>
	/// Orders records by a key selector. Operates on cached data.
	/// </summary>
	/// <typeparam name="TKey">The type of the key</typeparam>
	/// <param name="keySelector">The key selector function</param>
	/// <returns>Ordered records</returns>
	public IEnumerable<T> OrderBy<TKey>(Func<T, TKey> keySelector)
	{
		EnsureLoaded();
		return _cache?.Values.OrderBy(keySelector) ?? Enumerable.Empty<T>();
	}

	/// <summary>
	/// Orders records by a key selector in descending order. Operates on cached data.
	/// </summary>
	/// <typeparam name="TKey">The type of the key</typeparam>
	/// <param name="keySelector">The key selector function</param>
	/// <returns>Ordered records</returns>
	public IEnumerable<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
	{
		EnsureLoaded();
		return _cache?.Values.OrderByDescending(keySelector) ?? Enumerable.Empty<T>();
	}

	/// <summary>
	/// Reloads all records from disk, discarding the cache.
	/// </summary>
	/// <param name="cancellationToken">Optional token to cancel the operation</param>
	public async Task RefreshAsync(CancellationToken cancellationToken = default)
	{
		_isLoaded = false;
		_cache = null;
		_attachmentCache = null;
		await LoadAsync(cancellationToken);
	}

	/// <summary>
	/// Gets the count of records in the record set.
	/// </summary>
	/// <returns>The number of records</returns>
	public int Count()
	{
		EnsureLoaded();
		return _cache?.Count ?? 0;
	}

	private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
	{
		if (!_isLoaded)
		{
			await LoadAsync(cancellationToken);
		}
	}

	private void EnsureLoaded()
	{
		if (!_isLoaded)
		{
			throw new InvalidOperationException(
			$"RecordSet<{typeof(T).Name}> has not been loaded. Call LoadAsync() or GetAllAsync() first.");
		}
	}

	private async Task SaveAllAsync(CancellationToken cancellationToken = default)
	{
		if (_cache == null)
		{
			return;
		}

		var records = _cache.Values.ToList();
		await _store.SaveAsync(_fileName, records, cancellationToken: cancellationToken);
	}

	/// <summary>
	/// The store-level key under which a record's attachments are held.
	/// </summary>
	/// <remarks>
	/// A RecordSet keeps every record in one document keyed on the set's file name, so record IDs are
	/// only unique within the set. Attachments are therefore namespaced by the set, which keeps two
	/// sets that happen to share a record ID from colliding. The separator is a character that cannot
	/// appear in a type name, and is safe in a file name on every supported platform.
	/// </remarks>
	private string AttachmentKey(string recordId) => $"{_fileName}#{recordId}";

	private string GetId(T record)
	{
		return _idGetter(record) ?? throw new InvalidOperationException(
			$"Record of type {typeof(T).Name} has null ID value.");
	}

	private Func<T, string> CreateReflectionBasedIdGetter()
	{
		PropertyInfo idProperty;

		// If user specified, use that
		if (!string.IsNullOrEmpty(_options.IdPropertyName))
		{
			var specified = typeof(T).GetProperty(_options.IdPropertyName);
			if (specified == null)
			{
				throw new InvalidOperationException(
					$"Property '{_options.IdPropertyName}' not found on type {typeof(T).Name}.");
			}
			idProperty = specified;
		}
		else
		{
			// Try "Id"
			var idProp = typeof(T).GetProperty("Id");
			if (idProp != null)
			{
				idProperty = idProp;
			}
			else
			{
				// Try "{TypeName}Id"
				var typeNameId = typeof(T).GetProperty($"{typeof(T).Name}Id");
				if (typeNameId != null)
				{
					idProperty = typeNameId;
				}
				else
				{
					// Not found
					throw new InvalidOperationException(
						$"Could not find ID property on type {typeof(T).Name}. " +
						$"Expected 'Id' or '{typeof(T).Name}Id', or specify IdSelector in RecordSetOptions for AOT compatibility.");
				}
			}
		}

		// Create a delegate that uses the discovered property
		return record =>
		{
			var value = idProperty.GetValue(record);
			return value?.ToString() ?? string.Empty;
		};
	}
}
