namespace Cabinet.Core;

/// <summary>
/// Represents a file attachment that can be stored alongside a record.
/// Attachments are encrypted at rest and linked to their parent record.
/// </summary>
/// <param name="LogicalName">The logical file name for the attachment</param>
/// <param name="ContentType">The MIME type or content type of the attachment</param>
/// <param name="Content">The stream containing the attachment data</param>
public sealed record FileAttachment(string LogicalName, string ContentType, Stream Content);
