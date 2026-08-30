using System.Text.Json.Serialization;

namespace Cabinet.Core;

/// <summary>
/// A write-side handle to attachment content. Pass instances of this type to
/// <see cref="Cabinet.Abstractions.IOfflineStore.SaveAsync{T}"/> or
/// <see cref="Cabinet.Abstractions.IOfflineStore.SaveAttachmentAsync"/> to store the bytes as a
/// separate encrypted file.
/// </summary>
/// <remarks>
/// <para>
/// This type wraps a live <see cref="Stream"/> and therefore <strong>cannot be serialised</strong>.
/// It is not valid as a property on a record model; attempting to serialise it throws a
/// <see cref="NotSupportedException"/> with guidance. Use <see cref="AttachmentInfo"/> on your
/// models instead, and read the bytes back through
/// <see cref="Cabinet.Abstractions.IOfflineStore.OpenAttachmentAsync"/>.
/// </para>
/// <para>
/// The caller owns <see cref="Content"/> and is responsible for disposing it. The stream is read
/// from the current position to the end during a save, and is not disposed by the store.
/// </para>
/// </remarks>
[JsonConverter(typeof(FileAttachmentJsonConverter))]
public sealed class FileAttachment
{
	/// <summary>
	/// Initialises a new instance of the <see cref="FileAttachment"/> class with stream content.
	/// </summary>
	/// <param name="logicalName">The logical file name for the attachment (e.g. "photo.jpg")</param>
	/// <param name="contentType">The MIME type or content type of the attachment</param>
	/// <param name="content">The stream containing the attachment data</param>
	/// <exception cref="ArgumentException">Thrown if the logical name is not a valid attachment name</exception>
	public FileAttachment(string logicalName, string contentType, Stream content)
	{
		ArgumentNullException.ThrowIfNull(content);
		AttachmentName.Validate(logicalName, nameof(logicalName));

		LogicalName = logicalName;
		ContentType = contentType ?? string.Empty;
		Content     = content;
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="FileAttachment"/> class with byte array content.
	/// </summary>
	/// <param name="logicalName">The logical file name for the attachment (e.g. "photo.jpg")</param>
	/// <param name="contentType">The MIME type or content type of the attachment</param>
	/// <param name="content">The byte array containing the attachment data</param>
	/// <exception cref="ArgumentException">Thrown if the logical name is not a valid attachment name</exception>
	public FileAttachment(string logicalName, string contentType, byte[] content)
		: this(logicalName, contentType, new MemoryStream(content ?? throw new ArgumentNullException(nameof(content))))
	{
	}

	/// <summary>
	/// Gets the logical file name for the attachment. Unique within a single record.
	/// </summary>
	public string LogicalName { get; }

	/// <summary>
	/// Gets the MIME type or content type of the attachment.
	/// </summary>
	public string ContentType { get; }

	/// <summary>
	/// Gets the stream containing the attachment data. Owned by the caller.
	/// </summary>
	public Stream Content { get; }
}
