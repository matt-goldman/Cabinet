using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cabinet.Core;

/// <summary>
/// Fails fast when a <see cref="FileAttachment"/> is encountered during JSON serialisation,
/// with guidance on the supported pattern.
/// </summary>
/// <remarks>
/// Without this converter, System.Text.Json walks the wrapped <see cref="Stream"/>'s own properties
/// and fails deep inside the serialiser with an unrelated message (for example
/// "Timeouts are not supported on this stream", or an exception from <c>Stream.Length</c> on a
/// non-seekable stream). This turns that into an actionable error.
/// </remarks>
internal sealed class FileAttachmentJsonConverter : JsonConverter<FileAttachment>
{
	private const string Guidance =
		"FileAttachment holds a live Stream and cannot be serialised, so it is not valid as a property " +
		"on a record model. Use Cabinet.Core.AttachmentInfo on your model to carry the attachment's " +
		"name, content type and length, pass the FileAttachment to IOfflineStore.SaveAsync or " +
		"SaveAttachmentAsync to store the bytes, and call IOfflineStore.OpenAttachmentAsync to read " +
		"them back.";

	public override FileAttachment Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		=> throw new NotSupportedException(Guidance);

	public override void Write(Utf8JsonWriter writer, FileAttachment value, JsonSerializerOptions options)
		=> throw new NotSupportedException(Guidance);
}
