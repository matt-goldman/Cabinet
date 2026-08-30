using System.Text;
using Cabinet.Abstractions;
using Cabinet.Core;
using Cabinet.Security;

namespace Cabinet.Tests;

/// <summary>
/// Tests for attachment support on RecordSet&lt;T&gt;, which keys attachments on the record's own ID
/// rather than on the set's aggregate document, and cleans them up with the record.
/// </summary>
public class RecordSetAttachmentTests : IDisposable
{
	private readonly string _testDirectory;
	private readonly IOfflineStore _store;

	public RecordSetAttachmentTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"cabinet_recordset_attachments_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);

		var key = new byte[32];
		Random.Shared.NextBytes(key);
		_store = new FileOfflineStore(_testDirectory, new AesGcmEncryptionProvider(key));
	}

	public void Dispose()
	{
		if (Directory.Exists(_testDirectory))
		{
			Directory.Delete(_testDirectory, true);
		}

		GC.SuppressFinalize(this);
	}

	[Fact]
	public async Task AddAttachmentAsync_ShouldRoundTripContent()
	{
		var lessons = CreateSet<LessonRecord>();
		await lessons.AddAsync(new LessonRecord { Id = "lesson-1", Subject = "Science" });

		var content = Encoding.UTF8.GetBytes("photo bytes");
		var info = await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("photo.jpg", "image/jpeg", content));

		Assert.Equal("photo.jpg", info.Name);
		Assert.Equal(content.Length, info.Length);
		Assert.Equal(content, await ReadAsync(lessons, "lesson-1", "photo.jpg"));
	}

	[Fact]
	public async Task ListAttachmentsAsync_ShouldReturnMetadataForThatRecordOnly()
	{
		var lessons = CreateSet<LessonRecord>();
		await lessons.AddAsync(new LessonRecord { Id = "lesson-1" });
		await lessons.AddAsync(new LessonRecord { Id = "lesson-2" });

		await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("a.bin", "text/plain", new byte[] { 1 }));
		await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("b.bin", "text/plain", new byte[] { 2 }));
		await lessons.AddAttachmentAsync("lesson-2", new FileAttachment("c.bin", "text/plain", new byte[] { 3 }));

		Assert.Equal(["a.bin", "b.bin"], (await lessons.ListAttachmentsAsync("lesson-1")).Select(a => a.Name).Order());
		Assert.Equal(["c.bin"], (await lessons.ListAttachmentsAsync("lesson-2")).Select(a => a.Name));
	}

	[Fact]
	public async Task Attachments_ShouldBeScopedToTheirRecordSet()
	{
		// Two sets, the same record ID in each: the aggregate document is keyed on the type name, so
		// record IDs are only unique within a set.
		var lessons = CreateSet<LessonRecord>();
		var students = CreateSet<StudentRecord>();

		await lessons.AddAsync(new LessonRecord { Id = "shared-id" });
		await students.AddAsync(new StudentRecord { Id = "shared-id" });

		await lessons.AddAttachmentAsync("shared-id", new FileAttachment("file.bin", "text/plain", new byte[] { 1 }));
		await students.AddAttachmentAsync("shared-id", new FileAttachment("file.bin", "text/plain", new byte[] { 2 }));

		Assert.Equal([1], await ReadAsync(lessons, "shared-id", "file.bin"));
		Assert.Equal([2], await ReadAsync(students, "shared-id", "file.bin"));
	}

	[Fact]
	public async Task RemoveAsync_ShouldDeleteTheRecordsAttachments()
	{
		var lessons = CreateSet<LessonRecord>();
		await lessons.AddAsync(new LessonRecord { Id = "lesson-1" });
		await lessons.AddAsync(new LessonRecord { Id = "lesson-2" });

		await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("gone.bin", "text/plain", new byte[] { 1 }));
		await lessons.AddAttachmentAsync("lesson-2", new FileAttachment("kept.bin", "text/plain", new byte[] { 2 }));

		Assert.True(await lessons.RemoveAsync("lesson-1"));

		Assert.Empty(await lessons.ListAttachmentsAsync("lesson-1"));
		Assert.Null(await lessons.OpenAttachmentAsync("lesson-1", "gone.bin"));

		// The surviving record is untouched.
		Assert.Single(await lessons.ListAttachmentsAsync("lesson-2"));
		Assert.Equal([2], await ReadAsync(lessons, "lesson-2", "kept.bin"));
	}

	[Fact]
	public async Task RemoveAttachmentAsync_ShouldRemoveOnlyThatAttachment()
	{
		var lessons = CreateSet<LessonRecord>();
		await lessons.AddAsync(new LessonRecord { Id = "lesson-1" });

		await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("keep.bin", "text/plain", new byte[] { 1 }));
		await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("drop.bin", "text/plain", new byte[] { 2 }));

		Assert.True(await lessons.RemoveAttachmentAsync("lesson-1", "drop.bin"));
		Assert.False(await lessons.RemoveAttachmentAsync("lesson-1", "drop.bin"));

		var remaining = Assert.Single(await lessons.ListAttachmentsAsync("lesson-1"));
		Assert.Equal("keep.bin", remaining.Name);
	}

	[Fact]
	public async Task ListAttachmentsAsync_ShouldReflectAdditionsAndRemovals()
	{
		// Metadata is cached, so the cache must be invalidated by mutations.
		var lessons = CreateSet<LessonRecord>();
		await lessons.AddAsync(new LessonRecord { Id = "lesson-1" });

		Assert.Empty(await lessons.ListAttachmentsAsync("lesson-1"));

		await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("a.bin", "text/plain", new byte[] { 1 }));
		Assert.Single(await lessons.ListAttachmentsAsync("lesson-1"));

		await lessons.RemoveAttachmentAsync("lesson-1", "a.bin");
		Assert.Empty(await lessons.ListAttachmentsAsync("lesson-1"));
	}

	[Fact]
	public async Task ListAttachmentsAsync_AfterRefresh_ShouldReadFromDisk()
	{
		var lessons = CreateSet<LessonRecord>();
		await lessons.AddAsync(new LessonRecord { Id = "lesson-1" });
		await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("a.bin", "text/plain", new byte[] { 1 }));

		// Prime the metadata cache, then mutate through a second set over the same store.
		Assert.Single(await lessons.ListAttachmentsAsync("lesson-1"));

		var other = CreateSet<LessonRecord>();
		await other.RemoveAttachmentAsync("lesson-1", "a.bin");

		await lessons.RefreshAsync();
		Assert.Empty(await lessons.ListAttachmentsAsync("lesson-1"));
	}

	[Fact]
	public async Task AttachmentInfo_OnTheRecord_ShouldSurviveAggregateSave()
	{
		// The supported model shape: metadata inside the aggregate document, bytes outside it.
		var lessons = CreateSet<LessonRecord>();
		var lesson = new LessonRecord { Id = "lesson-1", Subject = "Art" };
		await lessons.AddAsync(lesson);

		var content = Encoding.UTF8.GetBytes("painting");
		lesson.Attachments = [await lessons.AddAttachmentAsync("lesson-1", new FileAttachment("art.png", "image/png", content))];
		await lessons.UpdateAsync("lesson-1", lesson);

		await lessons.RefreshAsync();

		var loaded = await lessons.GetByIdAsync("lesson-1");
		Assert.NotNull(loaded);

		var info = Assert.Single(loaded.Attachments!);
		Assert.Equal("art.png", info.Name);
		Assert.Equal("image/png", info.ContentType);
		Assert.Equal(content, await ReadAsync(lessons, "lesson-1", info.Name));
	}

	[Fact]
	public async Task CompactAttachmentsAsync_ShouldReclaimOrphansOnly()
	{
		var lessons = CreateSet<LessonRecord>();
		await lessons.AddAsync(new LessonRecord { Id = "live" });
		await lessons.AddAttachmentAsync("live", new FileAttachment("a.bin", "text/plain", new byte[] { 1 }));

		// An attachment for a record that was never added: what an interrupted RemoveAsync leaves.
		await lessons.AddAttachmentAsync("orphan", new FileAttachment("b.bin", "text/plain", new byte[] { 2 }));

		Assert.Equal(1, await lessons.CompactAttachmentsAsync());

		Assert.Single(await lessons.ListAttachmentsAsync("live"));
		Assert.Empty(await lessons.ListAttachmentsAsync("orphan"));

		// Idempotent.
		Assert.Equal(0, await lessons.CompactAttachmentsAsync());
	}

	[Fact]
	public async Task CompactAttachmentsAsync_ShouldNotTouchAnotherSetsAttachments()
	{
		var lessons = CreateSet<LessonRecord>();
		var students = CreateSet<StudentRecord>();

		await students.AddAsync(new StudentRecord { Id = "student-1" });
		await students.AddAttachmentAsync("student-1", new FileAttachment("a.bin", "text/plain", new byte[] { 1 }));

		// The lesson set knows nothing about student-1 and must not judge it an orphan.
		Assert.Equal(0, await lessons.CompactAttachmentsAsync());
		Assert.Single(await students.ListAttachmentsAsync("student-1"));
	}

	[Fact]
	public async Task CompactAttachmentsAsync_ShouldNotTouchAttachmentsSavedDirectlyToTheStore()
	{
		var lessons = CreateSet<LessonRecord>();
		await _store.SaveAttachmentAsync("standalone-record", new FileAttachment("a.bin", "text/plain", new byte[] { 1 }));

		Assert.Equal(0, await lessons.CompactAttachmentsAsync());
		Assert.Single(await _store.ListAttachmentsAsync("standalone-record"));
	}

	private RecordSet<TRecord> CreateSet<TRecord>() where TRecord : class, IHasId
		=> new(_store, new RecordSetOptions<TRecord> { IdSelector = r => r.Id });

	private static async Task<byte[]> ReadAsync<TRecord>(RecordSet<TRecord> set, string recordId, string name)
		where TRecord : class
	{
		await using var content = await set.OpenAttachmentAsync(recordId, name);
		Assert.NotNull(content);

		using var buffer = new MemoryStream();
		await content.CopyToAsync(buffer);
		return buffer.ToArray();
	}

	private interface IHasId
	{
		string Id { get; }
	}

	private class LessonRecord : IHasId
	{
		public string Id { get; set; } = string.Empty;
		public string Subject { get; set; } = string.Empty;
		public List<AttachmentInfo>? Attachments { get; set; }
	}

	private class StudentRecord : IHasId
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
	}
}
