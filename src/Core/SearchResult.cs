namespace Plugin.Maui.OfflineData.Core;

public sealed record SearchResult(
    string RecordId,
    double Score,
    RecordHeader Header);

public sealed record SearchResult<T>(
	string RecordId,
	double Score,
	RecordHeader Header,
	T Data);
