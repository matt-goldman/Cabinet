using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cabinet.Core;
using Cabinet.Security;

namespace Cabinet.Tests;

/// <summary>
/// Covers the attachment lifecycle: round-tripping content, manifest metadata, replacement,
/// deletion, and the failure modes that made attachments unusable before they were readable.
/// </summary>
public class AttachmentTests : IDisposable
{
	private readonly string _testRootPath;
	private readonly FileOfflineStore _store;

	public AttachmentTests()
	{
		_testRootPath = Path.Combine(Path.GetTempPath(), $"AttachmentTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testRootPath);

		var key = new byte[32];
		Random.Shared.NextBytes(key);
		_store = new FileOfflineStore(_testRootPath, new AesGcmEncryptionProvider(key));
	}

	public void Dispose()
	{
		if (Directory.Exists(_testRootPath))
		{
			Directory.Delete(_testRootPath, recursive: true);
		}

		GC.SuppressFinalize(this);
	}

	[Fact]
	public async Task ListAttachmentsAsync_ShouldReturnMetadata()
	{
		var content = Encoding.UTF8.GetBytes("hello world");
		await _store.SaveAsync("rec", new TestRecord(), [new FileAttachment("photo.jpg", "image/jpeg", content)]);

		var info = Assert.Single(await _store.ListAttachmentsAsync("rec"));

		Assert.Equal("photo.jpg", info.Name);
		Assert.Equal("image/jpeg", info.ContentType);
		Assert.Equal(content.Length, info.Length);
	}

	[Fact]
	public async Task ListAttachmentsAsync_WithNoAttachments_ShouldReturnEmpty()
	{
		await _store.SaveAsync("rec", new TestRecord());

		Assert.Empty(await _store.ListAttachmentsAsync("rec"));
	}

	[Fact]
	public async Task OpenAttachmentAsync_WithUnknownName_ShouldReturnNull()
	{
		await _store.SaveAsync("rec", new TestRecord(), [new FileAttachment("a.bin", "application/octet-stream", new byte[] { 1 })]);

		Assert.Null(await _store.OpenAttachmentAsync("rec", "missing.bin"));
	}

	[Fact]
	public async Task SaveAttachmentAsync_ShouldAddToExistingRecord()
	{
		await _store.SaveAsync("rec", new TestRecord(), [new FileAttachment("first.bin", "application/octet-stream", new byte[] { 1 })]);

		var info = await _store.SaveAttachmentAsync("rec", new FileAttachment("second.bin", "text/plain", new byte[] { 2, 2 }));

		Assert.Equal(2, info.Length);
		Assert.Equal(["first.bin", "second.bin"], (await _store.ListAttachmentsAsync("rec")).Select(a => a.Name).Order());
		Assert.Equal([1], await ReadAttachmentAsync("rec", "first.bin"));
		Assert.Equal([2, 2], await ReadAttachmentAsync("rec", "second.bin"));
	}

	[Fact]
	public async Task SaveAttachmentAsync_WithExistingName_ShouldReplace()
	{
		await _store.SaveAsync("rec", new TestRecord(), [new FileAttachment("a.bin", "text/plain", new byte[] { 1 })]);

		await _store.SaveAttachmentAsync("rec", new FileAttachment("a.bin", "image/png", new byte[] { 9, 9, 9 }));

		var info = Assert.Single(await _store.ListAttachmentsAsync("rec"));
		Assert.Equal("image/png", info.ContentType);
		Assert.Equal(3, info.Length);
		Assert.Equal([9, 9, 9], await ReadAttachmentAsync("rec", "a.bin"));
	}

	[Fact]
	public async Task DeleteAttachmentAsync_ShouldRemoveOnlyThatAttachment()
	{
		await _store.SaveAsync("rec", new TestRecord(),
		[
			new FileAttachment("keep.bin", "application/octet-stream", new byte[] { 1 }),
			new FileAttachment("drop.bin", "application/octet-stream", new byte[] { 2 }),
		]);

		Assert.True(await _store.DeleteAttachmentAsync("rec", "drop.bin"));

		var remaining = Assert.Single(await _store.ListAttachmentsAsync("rec"));
		Assert.Equal("keep.bin", remaining.Name);
		Assert.Null(await _store.OpenAttachmentAsync("rec", "drop.bin"));
		Assert.NotNull(await _store.OpenAttachmentAsync("rec", "keep.bin"));
	}

	[Fact]
	public async Task DeleteAttachmentAsync_WithUnknownName_ShouldReturnFalse()
	{
		await _store.SaveAsync("rec", new TestRecord(), [new FileAttachment("a.bin", "text/plain", new byte[] { 1 })]);

		Assert.False(await _store.DeleteAttachmentAsync("rec", "missing.bin"));
		Assert.Single(await _store.ListAttachmentsAsync("rec"));
	}

	[Fact]
	public async Task SaveAsync_WithNullAttachments_ShouldLeaveExistingAttachmentsUntouched()
	{
		await _store.SaveAsync("rec", new TestRecord { Name = "first" }, [new FileAttachment("a.bin", "text/plain", new byte[] { 1 })]);

		// A save that says nothing about attachments is not a statement that there are none.
		await _store.SaveAsync("rec", new TestRecord { Name = "second" });

		Assert.Single(await _store.ListAttachmentsAsync("rec"));
		Assert.Equal([1], await ReadAttachmentAsync("rec", "a.bin"));
		Assert.Equal("second", (await _store.LoadAsync<TestRecord>("rec"))!.Name);
	}

	[Fact]
	public async Task SaveAsync_WithEmptyAttachments_ShouldRemoveExistingAttachments()
	{
		await _store.SaveAsync("rec", new TestRecord(), [new FileAttachment("a.bin", "text/plain", new byte[] { 1 })]);

		await _store.SaveAsync("rec", new TestRecord(), []);

		Assert.Empty(await _store.ListAttachmentsAsync("rec"));
		Assert.Null(await _store.OpenAttachmentAsync("rec", "a.bin"));
	}

	[Fact]
	public async Task SaveAsync_WithReplacedAttachmentSet_ShouldSweepUnreferencedBlobs()
	{
		await _store.SaveAsync("rec", new TestRecord(),
		[
			new FileAttachment("old.bin", "text/plain", new byte[] { 1 }),
			new FileAttachment("kept.bin", "text/plain", new byte[] { 2 }),
		]);

		await _store.SaveAsync("rec", new TestRecord(),
		[
			new FileAttachment("kept.bin", "text/plain", new byte[] { 2 }),
			new FileAttachment("new.bin", "text/plain", new byte[] { 3 }),
		]);

		Assert.Equal(["kept.bin", "new.bin"], (await _store.ListAttachmentsAsync("rec")).Select(a => a.Name).Order());
		Assert.Null(await _store.OpenAttachmentAsync("rec", "old.bin"));

		// The dropped blob is gone from disk, not merely unlisted: one manifest plus two blobs.
		var directory = Directory.GetDirectories(Path.Combine(_testRootPath, "attachments")).Single();
		Assert.Equal(3, Directory.GetFiles(directory).Length);
	}

	[Fact]
	public async Task SaveAsync_WithDuplicateAttachmentNames_ShouldThrow()
	{
		var attachments = new[]
		{
			new FileAttachment("same.bin", "text/plain", new byte[] { 1 }),
			new FileAttachment("same.bin", "text/plain", new byte[] { 2 }),
		};

		await Assert.ThrowsAsync<ArgumentException>(() => _store.SaveAsync("rec", new TestRecord(), attachments));
	}

	[Fact]
	public async Task SaveAsync_WithNonSeekableStream_ShouldSucceed()
	{
		// Reproduces the Android asset-stream case: Length and Position both throw.
		var content = Encoding.UTF8.GetBytes("non-seekable content");
		using var stream = new NonSeekableStream(content);

		await _store.SaveAsync("rec", new TestRecord(), [new FileAttachment("asset.png", "image/png", stream)]);

		Assert.Equal(content, await ReadAttachmentAsync("rec", "asset.png"));
		Assert.Equal(content.Length, Assert.Single(await _store.ListAttachmentsAsync("rec")).Length);
	}

	[Theory]
	[InlineData("../escape.bin")]
	[InlineData("..\\escape.bin")]
	[InlineData("nested/name.bin")]
	[InlineData("..")]
	[InlineData("")]
	[InlineData("   ")]
	public void FileAttachment_WithUnsafeName_ShouldThrow(string name)
	{
		Assert.Throws<ArgumentException>(() => new FileAttachment(name, "text/plain", new byte[] { 1 }));
	}

	[Fact]
	public async Task OpenAttachmentAsync_WithUnsafeName_ShouldThrow()
	{
		await _store.SaveAsync("rec", new TestRecord(), [new FileAttachment("a.bin", "text/plain", new byte[] { 1 })]);

		await Assert.ThrowsAsync<ArgumentException>(() => _store.OpenAttachmentAsync("rec", "../../records/rec.dat"));
	}

	[Fact]
	public async Task DeleteAsync_WithPrefixCollidingIds_ShouldNotAffectOtherRecords()
	{
		// "rec-1" is a prefix of "rec-12": a glob over a flat attachments directory would take both.
		await _store.SaveAsync("rec-1", new TestRecord(), [new FileAttachment("a.bin", "text/plain", new byte[] { 1 })]);
		await _store.SaveAsync("rec-12", new TestRecord(), [new FileAttachment("a.bin", "text/plain", new byte[] { 2 })]);

		await _store.DeleteAsync("rec-1");

		Assert.Empty(await _store.ListAttachmentsAsync("rec-1"));
		Assert.Single(await _store.ListAttachmentsAsync("rec-12"));
		Assert.Equal([2], await ReadAttachmentAsync("rec-12", "a.bin"));
	}

	[Fact]
	public async Task OpenAttachmentAsync_WithBlobSwappedBetweenRecords_ShouldFailAuthentication()
	{
		// Attachment blobs are authenticated against both the record id and the attachment name,
		// so a blob lifted from another record must not decrypt.
		var attachments = Path.Combine(_testRootPath, "attachments");

		await _store.SaveAsync("rec-a", new TestRecord(), [new FileAttachment("a.bin", "text/plain", new byte[] { 1 })]);
		var directoryA = Directory.GetDirectories(attachments).Single();

		await _store.SaveAsync("rec-b", new TestRecord(), [new FileAttachment("a.bin", "text/plain", new byte[] { 2 })]);
		var directoryB = Directory.GetDirectories(attachments).Single(d => d != directoryA);

		File.Copy(
			Directory.GetFiles(directoryB, "*.bin").Single(),
			Directory.GetFiles(directoryA, "*.bin").Single(),
			overwrite: true);

		await Assert.ThrowsAnyAsync<CryptographicException>(() => _store.OpenAttachmentAsync("rec-a", "a.bin"));

		// The record the blob legitimately belongs to is unaffected.
		Assert.Equal([2], await ReadAttachmentAsync("rec-b", "a.bin"));
	}

	[Fact]
	public void FileAttachment_OnARecordModel_ShouldThrowWithGuidance()
	{
		// The failure this replaces surfaced as "Timeouts are not supported on this stream", or as
		// an exception from Stream.Length on a non-seekable stream, deep inside the serialiser.
		var model = new ModelWithAttachment
		{
			Photo = new FileAttachment("photo.jpg", "image/jpeg", new byte[] { 1, 2, 3 }),
		};

		var exception = Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(model));
		Assert.Contains("AttachmentInfo", exception.Message);
	}

	[Fact]
	public async Task AttachmentInfo_OnARecordModel_ShouldRoundTrip()
	{
		// The supported pattern: metadata on the record, bytes in the attachment store.
		var content = Encoding.UTF8.GetBytes("photo bytes");
		var attachment = new FileAttachment("photo.jpg", "image/jpeg", content);
		var info = await _store.SaveAttachmentAsync("rec", attachment);

		await _store.SaveAsync("rec", new ModelWithInfo { Attachments = [info] });

		var loaded = await _store.LoadAsync<ModelWithInfo>("rec");
		Assert.NotNull(loaded);

		var loadedInfo = Assert.Single(loaded.Attachments);
		Assert.Equal("photo.jpg", loadedInfo.Name);
		Assert.Equal("image/jpeg", loadedInfo.ContentType);
		Assert.Equal(content.Length, loadedInfo.Length);
		Assert.Equal(content, await ReadAttachmentAsync("rec", loadedInfo.Name));
	}

	private async Task<byte[]> ReadAttachmentAsync(string id, string name)
	{
		await using var content = await _store.OpenAttachmentAsync(id, name);
		Assert.NotNull(content);

		using var buffer = new MemoryStream();
		await content.CopyToAsync(buffer);
		return buffer.ToArray();
	}

	private class TestRecord
	{
		public string Name { get; set; } = string.Empty;
	}

	private class ModelWithAttachment
	{
		public FileAttachment? Photo { get; set; }
	}

	private class ModelWithInfo
	{
		public List<AttachmentInfo> Attachments { get; set; } = [];
	}

	/// <summary>
	/// A read-only stream that refuses Length and Position, as platform asset and network streams do.
	/// </summary>
	private sealed class NonSeekableStream(byte[] content) : Stream
	{
		private readonly MemoryStream _inner = new(content);

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
		public override void Flush() => _inner.Flush();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if (disposing) _inner.Dispose();
			base.Dispose(disposing);
		}
	}
}
