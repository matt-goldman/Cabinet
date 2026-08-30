using Cabinet;
using Cabinet.Core;

namespace demo.Models;

/// <summary>
/// Represents a student with profile information and optional photo attachment.
/// Demonstrates using FileAttachment as a property on the record.
/// </summary>
[AotRecord]
public class StudentRecord
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int Age { get; set; }
	public string Grade { get; set; } = string.Empty;
	public List<string> Subjects { get; set; } = [];
	
	/// <summary>
	/// Metadata for the student's profile photo. The bytes are stored as a separate encrypted
	/// attachment, keyed on this record's Id, and read back with IOfflineStore.OpenAttachmentAsync.
	/// </summary>
	public AttachmentInfo? ProfilePhoto { get; set; }
	
	/// <summary>
	/// Base64-encoded certificate (custom encoding example).
	/// This demonstrates storing binary data that needs custom encoding/decoding.
	/// </summary>
	public string? CertificateBase64 { get; set; }
	
	public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
}
