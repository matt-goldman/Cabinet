using Cabinet;
using Cabinet.Core;

namespace demo.Models;

[AotRecord]
public class LessonRecord
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
	public string Subject { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public List<string> Children { get; set; } = [];
	public List<string> Tags { get; set; } = [];
	/// <summary>
	/// Metadata for this lesson's attachments. The bytes live in the attachment store; read them
	/// back with IOfflineStore.OpenAttachmentAsync using this record's Id and the attachment name.
	/// </summary>
	public List<AttachmentInfo>? Attachments { get; set; }
}
