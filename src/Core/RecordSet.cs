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
	public async Task LoadAsync()
	{
		var records = await _store.LoadAsync<List<T>>(_fileName);

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
	/// <returns>All records of type T</returns>
	public async Task<IEnumerable<T>> GetAllAsync()
	{
		await EnsureLoadedAsync();
		return _cache?.Values.ToList() ?? [];
	}

	/// <summary>
	/// Gets a record by its ID. Loads from disk if not already loaded.
	/// </summary>
	/// <param name="id">The ID of the record to retrieve</param>
	/// <returns>The record if found, null otherwise</returns>
	public async Task<T?> GetByIdAsync(string id)
	{
		await EnsureLoadedAsync();
		return _cache?.GetValueOrDefault(id);
	}

	/// <summary>
	/// Adds a new record to the record set. Automatically persists to disk.
	/// </summary>
	/// <param name="record">The record to add</param>
	public async Task AddAsync(T record)
	{
		ArgumentNullException.ThrowIfNull(record);

		await EnsureLoadedAsync();

		var id = GetId(record);

		// Add to cache
		if (_cache != null)
		{
			_cache[id] = record;
		}

		// Persist to disk
		await SaveAllAsync();
	}

	/// <summary>
	/// Updates an existing record. Automatically persists to disk.
	/// </summary>
	/// <param name="id">The ID of the record to update</param>
	/// <param name="record">The updated record</param>
	/// <returns>True if the record was found and updated, false otherwise</returns>
	public async Task<bool> UpdateAsync(string id, T record)
	{
		ArgumentNullException.ThrowIfNull(record);

		await EnsureLoadedAsync();

		if (_cache == null || !_cache.ContainsKey(id))
		{
			return false;
		}

		_cache[id] = record;
		await SaveAllAsync();
		return true;
	}

	/// <summary>
	/// Removes a record by its ID. Automatically persists to disk.
	/// </summary>
	/// <param name="id">The ID of the record to remove</param>
	/// <returns>True if the record was found and removed, false otherwise</returns>
	public async Task<bool> RemoveAsync(string id)
	{
		await EnsureLoadedAsync();

		if (_cache == null || !_cache.Remove(id))
		{
			return false;
		}

		await SaveAllAsync();
		return true;
	}

	/// <summary>
	/// Searches records using the encrypted index.
	/// </summary>
	/// <param name="terms">Search terms to match</param>
	/// <returns>Records matching the search terms</returns>
	public async Task<IEnumerable<T>> FindAsync(params string[] terms)
	{
		if (terms.Length == 0)
		{
			return await GetAllAsync();
		}

		var query = string.Join(" ", terms);
		var results = await _store.FindAsync<T>(query);

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
	public async Task RefreshAsync()
	{
		_isLoaded = false;
		_cache = null;
		await LoadAsync();
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

	private async Task EnsureLoadedAsync()
	{
		if (!_isLoaded)
		{
			await LoadAsync();
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

	private async Task SaveAllAsync()
	{
		if (_cache == null)
		{
			return;
		}

		var records = _cache.Values.ToList();
		await _store.SaveAsync(_fileName, records);
	}

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
