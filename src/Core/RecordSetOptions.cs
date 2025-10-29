namespace Cabinet.Core;

/// <summary>
/// Configuration options for RecordSet behavior.
/// </summary>
/// <typeparam name="T">The type of records in the set</typeparam>
public sealed class RecordSetOptions<T> where T : class
{
	/// <summary>
	/// Custom file name to use instead of the type name.
	/// Default: typeof(T).Name (e.g., "Lesson" for LessonRecord)
	/// </summary>
	public string? CustomFileName { get; set; }

	/// <summary>
	/// Function to extract the ID from a record.
	/// This is the AOT-safe approach - provide a lambda like: r => r.Id
	/// If not specified, falls back to reflection-based discovery (not AOT-compatible).
	/// </summary>
	/// <remarks>
	/// For AOT compatibility, always specify this:
	/// <code>
	/// var options = new RecordSetOptions&lt;LessonRecord&gt;
	/// {
	///     IdSelector = lesson => lesson.LessonId
	/// };
	/// </code>
	/// </remarks>
	public Func<T, string>? IdSelector { get; set; }

	/// <summary>
	/// Name of the property to use as the record ID (reflection-based, not AOT-safe).
	/// Only used if IdSelector is not provided.
	/// Default: Auto-discovers "Id" or "{TypeName}Id"
	/// </summary>
	public string? IdPropertyName { get; set; }

	/// <summary>
	/// Whether to cache loaded records in memory.
	/// Default: true (for fast queries on small-to-medium datasets)
	/// </summary>
	public bool EnableCaching { get; set; } = true;
}
