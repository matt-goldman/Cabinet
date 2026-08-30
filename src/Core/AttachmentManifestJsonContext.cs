using System.Text.Json.Serialization;

namespace Cabinet.Core;

/// <summary>
/// AOT-safe serialisation context for the attachment manifest.
/// </summary>
/// <remarks>
/// The manifest is Cabinet's own document, so it is serialised with this context rather than the
/// caller-supplied <see cref="System.Text.Json.JsonSerializerOptions"/>. A caller's source-generated
/// context only knows about their record types, and would fail on <see cref="AttachmentInfo"/> under AOT.
/// </remarks>
[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(List<AttachmentInfo>))]
internal sealed partial class AttachmentManifestJsonContext : JsonSerializerContext
{
}
