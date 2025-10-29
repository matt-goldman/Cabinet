using Cabinet.Abstractions;

namespace Cabinet.Core;

/// <summary>
/// Represents a managed collection of records stored under a single identifier.
/// This provides a scoped view of multiple records as a logical unit.
/// </summary>
/// <typeparam name="T">The type of records in the collection</typeparam>
/// <remarks>
/// RecordCollection allows you to treat a group of related records as a single collection,
/// stored and retrieved together under one ID. This is useful for scenarios like:
/// - All lessons for a specific subject/year
/// - All records within a bounded context
/// - Collections that should be loaded/saved atomically
/// </remarks>
/// <example>
/// <code>
/// var mathsLessons = new RecordCollection&lt;LessonRecord&gt;(store, "Maths_2025");
/// await mathsLessons.AddAsync(newLesson);
/// var allLessons = await mathsLessons.GetAllAsync();
/// var filtered = await mathsLessons.FindAsync("Dylan", "seagulls");
/// </code>
/// </example>
public sealed class RecordCollection<T>
{
	private readonly IOfflineStore _store;
	private readonly string _recordId;

	/// <summary>
	/// Creates a new record collection scoped to a specific identifier.
	/// </summary>
	/// <param name="store">The offline store to use</param>
	/// <param name="recordId">The identifier for this collection</param>
	public RecordCollection(IOfflineStore store, string recordId)
	{
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_recordId = recordId ?? throw new ArgumentNullException(nameof(recordId));
	}

	/// <summary>
	/// Adds a record to this collection.
	/// </summary>
	/// <param name="record">The record to add</param>
	/// <remarks>
	/// This loads the entire collection, adds the new record, and saves it back.
	/// For large collections, consider using individual record storage instead.
	/// </remarks>
	public async Task AddAsync(T record)
	{
		var existing = await _store.LoadAsync<List<T>>(_recordId) ?? new List<T>();
		existing.Add(record);
		await _store.SaveAsync(_recordId, existing);
	}

	/// <summary>
	/// Gets all records in this collection.
	/// </summary>
	/// <returns>All records in the collection</returns>
	public async Task<IEnumerable<T>> GetAllAsync()
	{
		var collection = await _store.LoadAsync<List<T>>(_recordId);
		return collection ?? Enumerable.Empty<T>();
	}

	/// <summary>
	/// Searches for records in this collection matching the specified terms.
	/// </summary>
	/// <param name="terms">Search terms to match</param>
	/// <returns>Matching records from this collection</returns>
	/// <remarks>
	/// This performs a full-text search using the store's index and filters
	/// results to only include records from this collection's scope.
	/// </remarks>
	public async Task<IEnumerable<T>> FindAsync(params string[] terms)
	{
		var query = string.Join(" ", terms);
		var results = await _store.FindAsync<T>(query);
		return results
			.Where(r => r.RecordId == _recordId)
			.Select(r => r.Data);
	}

	/// <summary>
	/// Removes a record from this collection.
	/// </summary>
	/// <param name="predicate">Predicate to identify the record to remove</param>
	/// <returns>True if a record was removed, false otherwise</returns>
	public async Task<bool> RemoveAsync(Func<T, bool> predicate)
	{
		var existing = await _store.LoadAsync<List<T>>(_recordId);
		if (existing == null)
			return false;

		var toRemove = existing.FirstOrDefault(predicate);
		if (toRemove == null)
			return false;

		existing.Remove(toRemove);
		await _store.SaveAsync(_recordId, existing);
		return true;
	}

	/// <summary>
	/// Updates a record in this collection.
	/// </summary>
	/// <param name="predicate">Predicate to identify the record to update</param>
	/// <param name="updater">Function to update the record</param>
	/// <returns>True if a record was updated, false otherwise</returns>
	public async Task<bool> UpdateAsync(Func<T, bool> predicate, Action<T> updater)
	{
		var existing = await _store.LoadAsync<List<T>>(_recordId);
		if (existing == null)
			return false;

		var toUpdate = existing.FirstOrDefault(predicate);
		if (toUpdate == null)
			return false;

		updater(toUpdate);
		await _store.SaveAsync(_recordId, existing);
		return true;
	}

	/// <summary>
	/// Clears all records from this collection.
	/// </summary>
	public async Task ClearAsync()
	{
		await _store.SaveAsync(_recordId, new List<T>());
	}

	/// <summary>
	/// Gets the count of records in this collection.
	/// </summary>
	/// <returns>The number of records</returns>
	public async Task<int> CountAsync()
	{
		var collection = await _store.LoadAsync<List<T>>(_recordId);
		return collection?.Count ?? 0;
	}
}
