namespace Plugin.Maui.OfflineData.Core;

/// <summary>
/// Represents metadata about a stored record, including its identifier, creation timestamp, and custom metadata.
/// </summary>
/// <param name="Id">The unique identifier of the record</param>
/// <param name="Created">The timestamp when the record was created</param>
/// <param name="Metadata">Optional custom metadata key-value pairs associated with the record</param>
public sealed record RecordHeader(
    string Id,
    DateTimeOffset Created,
    IDictionary<string, string>? Metadata = null);
