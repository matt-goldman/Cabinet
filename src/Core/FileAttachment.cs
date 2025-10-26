namespace Plugin.Maui.OfflineData.Core;

public sealed record FileAttachment(string LogicalName, string ContentType, Stream Content);
