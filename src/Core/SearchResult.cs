namespace Plugin.Maui.OfflineData.Core;

public sealed record SearchResult(
    string RecordId,
    double Score,
    RecordHeader Header);
