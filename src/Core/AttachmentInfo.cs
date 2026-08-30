namespace Cabinet.Core;

/// <summary>
/// Serialisable metadata describing an attachment stored alongside a record.
/// This is the type to use as a property on your record models — it carries the
/// attachment's identity and content type, but never its bytes.
/// </summary>
/// <param name="Name">The logical file name of the attachment (e.g. "photo.jpg")</param>
/// <param name="ContentType">The MIME type or content type of the attachment</param>
/// <param name="Length">The length, in bytes, of the attachment content</param>
/// <remarks>
/// <para>
/// Attachment bytes are stored as separate encrypted files, not inside the record's JSON.
/// Use <see cref="Cabinet.Abstractions.IOfflineStore.OpenAttachmentAsync"/> to read the
/// content back when you need it.
/// </para>
/// <para>
/// Do not put <see cref="FileAttachment"/> on a record model — it wraps a live
/// <see cref="Stream"/> and cannot be serialised. Use this type instead.
/// </para>
/// </remarks>
public sealed record AttachmentInfo(string Name, string ContentType, long Length);
