namespace Cabinet.Core;

/// <summary>
/// Represents a search result containing the record identifier, relevance score, and metadata.
/// </summary>
/// <param name="RecordId">The unique identifier of the matching record</param>
/// <param name="Score">The relevance score indicating how well the record matches the search query</param>
/// <param name="Header">The record header containing creation timestamp and metadata</param>
public sealed record SearchResult(
    string RecordId,
    double Score,
    RecordHeader Header);

/// <summary>
/// Represents a typed search result containing the record data along with metadata.
/// </summary>
/// <typeparam name="T">The type of the record data</typeparam>
/// <param name="RecordId">The unique identifier of the matching record</param>
/// <param name="Score">The relevance score indicating how well the record matches the search query</param>
/// <param name="Header">The record header containing creation timestamp and metadata</param>
/// <param name="Data">The deserialized record data</param>
public sealed record SearchResult<T>(
	string RecordId,
	double Score,
	RecordHeader Header,
	T Data);
