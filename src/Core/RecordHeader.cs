namespace Plugin.Maui.OfflineData.Core;

public sealed record RecordHeader(
    string Id,
    DateTimeOffset Created,
    IDictionary<string, string>? Metadata = null);
