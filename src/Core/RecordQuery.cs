namespace Cabinet.Core;

/// <summary>
/// Represents a queryable collection of records from the offline store.
/// Provides LINQ-style fluent operations with deferred execution.
/// </summary>
/// <typeparam name="T">The type of records in the query</typeparam>
/// <remarks>
/// Creates a new record query from an enumerable source.
/// </remarks>
/// <param name="source">The source collection of records</param>
public sealed class RecordQuery<T>(IEnumerable<T> source)
{
    /// <summary>
    /// Filters the record query based on a predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate</param>
    /// <returns>A new record query with filtered records</returns>
    public RecordQuery<T> Where(Func<T, bool> predicate)
		=> new(source.Where(predicate));

	/// <summary>
	/// Projects each record to a new form.
	/// </summary>
	/// <typeparam name="TResult">The type of the result</typeparam>
	/// <param name="selector">The projection function</param>
	/// <returns>A new record query with projected records</returns>
	public RecordQuery<TResult> Select<TResult>(Func<T, TResult> selector)
		=> new(source.Select(selector));

	/// <summary>
	/// Sorts the records in ascending order by a key.
	/// </summary>
	/// <typeparam name="TKey">The type of the key</typeparam>
	/// <param name="keySelector">The key selector function</param>
	/// <returns>A new record query with sorted records</returns>
	public RecordQuery<T> OrderBy<TKey>(Func<T, TKey> keySelector)
		=> new(source.OrderBy(keySelector));

	/// <summary>
	/// Sorts the records in descending order by a key.
	/// </summary>
	/// <typeparam name="TKey">The type of the key</typeparam>
	/// <param name="keySelector">The key selector function</param>
	/// <returns>A new record query with sorted records</returns>
	public RecordQuery<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
		=> new(source.OrderByDescending(keySelector));

	/// <summary>
	/// Bypasses a specified number of records.
	/// </summary>
	/// <param name="count">The number of records to skip</param>
	/// <returns>A new record query with remaining records</returns>
	public RecordQuery<T> Skip(int count)
		=> new(source.Skip(count));

	/// <summary>
	/// Takes a specified number of records.
	/// </summary>
	/// <param name="count">The number of records to take</param>
	/// <returns>A new record query with limited records</returns>
	public RecordQuery<T> Take(int count)
		=> new(source.Take(count));

	/// <summary>
	/// Returns the first record, or throws if the sequence is empty.
	/// </summary>
	/// <returns>The first record</returns>
	public T First()
		=> source.First();

	/// <summary>
	/// Returns the first record, or default if the sequence is empty.
	/// </summary>
	/// <returns>The first record or default</returns>
	public T? FirstOrDefault()
		=> source.FirstOrDefault();

	/// <summary>
	/// Returns the only record, or throws if there is not exactly one record.
	/// </summary>
	/// <returns>The single record</returns>
	public T Single()
		=> source.Single();

	/// <summary>
	/// Returns the only record, or default if the sequence is empty or has multiple records.
	/// </summary>
	/// <returns>The single record or default</returns>
	public T? SingleOrDefault()
		=> source.SingleOrDefault();

	/// <summary>
	/// Determines whether any records match a predicate.
	/// </summary>
	/// <param name="predicate">The predicate to test</param>
	/// <returns>True if any records match, false otherwise</returns>
	public bool Any(Func<T, bool> predicate)
		=> source.Any(predicate);

	/// <summary>
	/// Determines whether any records exist.
	/// </summary>
	/// <returns>True if any records exist, false otherwise</returns>
	public bool Any()
		=> source.Any();

	/// <summary>
	/// Determines whether all records match a predicate.
	/// </summary>
	/// <param name="predicate">The predicate to test</param>
	/// <returns>True if all records match, false otherwise</returns>
	public bool All(Func<T, bool> predicate)
		=> source.All(predicate);

	/// <summary>
	/// Counts the number of records.
	/// </summary>
	/// <returns>The total count</returns>
	public int Count()
		=> source.Count();

	/// <summary>
	/// Counts the number of records matching a predicate.
	/// </summary>
	/// <param name="predicate">The predicate to test</param>
	/// <returns>The count of matching records</returns>
	public int Count(Func<T, bool> predicate)
		=> source.Count(predicate);

	/// <summary>
	/// Converts the record query to a list.
	/// </summary>
	/// <returns>A list containing all records</returns>
	public List<T> ToList()
		=> [.. source];

	/// <summary>
	/// Converts the record query to an array.
	/// </summary>
	/// <returns>An array containing all records</returns>
	public T[] ToArray()
		=> [.. source];

	/// <summary>
	/// Gets the underlying enumerable source.
	/// </summary>
	/// <returns>The source enumerable</returns>
	public IEnumerable<T> AsEnumerable()
		=> source;
}
