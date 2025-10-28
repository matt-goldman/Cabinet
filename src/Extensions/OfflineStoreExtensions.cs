using Plugin.Maui.OfflineData.Abstractions;
using Plugin.Maui.OfflineData.Core;

namespace Plugin.Maui.OfflineData.Extensions;

/// <summary>
/// Extension methods for IOfflineStore that provide LINQ-style querying capabilities.
/// These methods combine index-based lookups with in-memory LINQ filtering for flexible queries.
/// </summary>
public static class OfflineStoreExtensions
{
	/// <summary>
	/// Finds records matching the specified search terms and returns their data as a queryable record set.
	/// This is a convenience method that combines FindAsync with data extraction.
	/// When multiple terms are provided, performs an OR search (records matching any term).
	/// </summary>
	/// <typeparam name="T">The type of records to find</typeparam>
	/// <param name="store">The offline store to query</param>
	/// <param name="terms">Search terms to match (OR operation)</param>
	/// <returns>A queryable record set of matching records</returns>
	/// <example>
	/// <code>
	/// var lessons = await store
	///     .FindManyAsync&lt;LessonRecord&gt;("maths", "Dylan", "Jessica")
	///     .Where(l => l.Subject == "Maths")
	///     .OrderBy(l => l.Date)
	///     .ToList();
	/// </code>
	/// </example>
	public static async Task<RecordSet<T>> FindManyAsync<T>(
		this IOfflineStore store,
		params string[] terms)
	{
		if (terms.Length == 0)
			return new RecordSet<T>(Enumerable.Empty<T>());

		if (terms.Length == 1)
		{
			var results = await store.FindAsync<T>(terms[0]);
			return new RecordSet<T>(results.Select(r => r.Data));
		}

		// For multiple terms, perform separate queries and combine results
		var allResults = new Dictionary<string, T>(); // Use dictionary to deduplicate by ID
		foreach (var term in terms)
		{
			var results = await store.FindAsync<T>(term);
			foreach (var result in results)
			{
				allResults[result.RecordId] = result.Data;
			}
		}

		return new RecordSet<T>(allResults.Values);
	}

	/// <summary>
	/// Applies an additional predicate filter to a record set.
	/// This is syntactic sugar over RecordSet's Where() for clearer intent in chained queries.
	/// </summary>
	/// <typeparam name="T">The type of items in the record set</typeparam>
	/// <param name="source">The source record set</param>
	/// <param name="predicate">The filter predicate</param>
	/// <returns>Filtered record set</returns>
	/// <example>
	/// <code>
	/// var results = await store
	///     .FindManyAsync&lt;LessonRecord&gt;("seagulls")
	///     .WhereMatch(r => r.Children.Contains("Dylan"))
	///     .WhereMatch(r => r.Date.Year == 2025)
	///     .ToList();
	/// </code>
	/// </example>
	public static RecordSet<T> WhereMatch<T>(
		this RecordSet<T> source,
		Func<T, bool> predicate)
		=> source.Where(predicate);

	/// <summary>
	/// Applies an additional predicate filter to a collection.
	/// This is syntactic sugar over LINQ's Where() for clearer intent in chained queries.
	/// </summary>
	/// <typeparam name="T">The type of items in the collection</typeparam>
	/// <param name="source">The source collection</param>
	/// <param name="predicate">The filter predicate</param>
	/// <returns>Filtered enumerable</returns>
	/// <example>
	/// <code>
	/// var results = await store
	///     .FindManyAsync&lt;LessonRecord&gt;("seagulls")
	///     .WhereMatch(r => r.Children.Contains("Dylan"))
	///     .WhereMatch(r => r.Date.Year == 2025)
	///     .ToList();
	/// </code>
	/// </example>
	public static IEnumerable<T> WhereMatch<T>(
		this IEnumerable<T> source,
		Func<T, bool> predicate)
		=> source.Where(predicate);

	/// <summary>
	/// Finds records matching search terms and immediately applies a predicate filter.
	/// This combines index lookup with in-memory filtering in a single operation.
	/// </summary>
	/// <typeparam name="T">The type of records to find</typeparam>
	/// <param name="store">The offline store to query</param>
	/// <param name="predicate">The filter predicate to apply</param>
	/// <param name="terms">Search terms to match (OR operation)</param>
	/// <returns>A queryable record set of matching, filtered records</returns>
	/// <example>
	/// <code>
	/// var mathsLessons = await store
	///     .FindWhereAsync&lt;LessonRecord&gt;(
	///         l => l.Subject == "Maths" &amp;&amp; l.Children.Contains("Dylan"),
	///         "seagulls", "volcano", "experiment")
	///     .OrderBy(l => l.Date)
	///     .ToList();
	/// </code>
	/// </example>
	public static async Task<RecordSet<T>> FindWhereAsync<T>(
		this IOfflineStore store,
		Func<T, bool> predicate,
		params string[] terms)
	{
		var results = await store.FindManyAsync<T>(terms);
		return results.Where(predicate);
	}
}
