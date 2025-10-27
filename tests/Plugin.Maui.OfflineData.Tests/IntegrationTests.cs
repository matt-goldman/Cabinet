using Plugin.Maui.OfflineData.Core;
using Plugin.Maui.OfflineData.Security;

namespace Plugin.Maui.OfflineData.Tests;

/// <summary>
/// Integration tests that validate end-to-end scenarios for the offline data store.
/// These tests simulate realistic usage patterns.
/// </summary>
public class IntegrationTests : IDisposable
{
	private readonly string _testRootPath;
	private readonly byte[] _testKey;

	public IntegrationTests()
	{
		_testRootPath = Path.Combine(Path.GetTempPath(), $"IntegrationTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testRootPath);

		_testKey = new byte[32];
		Random.Shared.NextBytes(_testKey);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testRootPath))
		{
			Directory.Delete(_testRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task EndToEnd_SaveLoadDelete_Workflow()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		var lessonRecord = new LessonRecord
		{
			Subject = "Science",
			Date = DateTimeOffset.Parse("2025-10-27T00:00:00Z"),
			Description = "Observed seagulls at the beach during low tide",
			Tags = new List<string> { "nature", "birds", "marine-life" }
		};

		// Act & Assert - Save
		await store.SaveAsync("lesson-2025-10-27", lessonRecord);
		Assert.True(File.Exists(Path.Combine(_testRootPath, "records", "lesson-2025-10-27.dat")));

		// Act & Assert - Load
		var loaded = await store.LoadAsync<LessonRecord>("lesson-2025-10-27");
		Assert.NotNull(loaded);
		Assert.Equal("Science", loaded.Subject);
		Assert.Equal("Observed seagulls at the beach during low tide", loaded.Description);
		Assert.Equal(3, loaded.Tags.Count);

		// Act & Assert - Delete
		await store.DeleteAsync("lesson-2025-10-27");
		Assert.False(File.Exists(Path.Combine(_testRootPath, "records", "lesson-2025-10-27.dat")));

		// Verify load returns null after delete
		var deletedRecord = await store.LoadAsync<LessonRecord>("lesson-2025-10-27");
		Assert.Null(deletedRecord);
	}

	[Fact]
	public async Task EndToEnd_WithAttachments_Workflow()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		var lessonRecord = new LessonRecord
		{
			Subject = "Photography",
			Date = DateTimeOffset.UtcNow,
			Description = "Beach photography session"
		};

		// Create mock photo attachment
		var photoBytes = new byte[1024];
		Random.Shared.NextBytes(photoBytes);
		var photoStream = new MemoryStream(photoBytes);
		var photoAttachment = new FileAttachment("beach-photo.jpg", "image/jpeg", photoStream);

		// Act - Save with attachment
		await store.SaveAsync("photo-lesson-1", lessonRecord, new[] { photoAttachment });

		// Assert - Verify record and attachment saved
		Assert.True(File.Exists(Path.Combine(_testRootPath, "records", "photo-lesson-1.dat")));
		Assert.True(File.Exists(Path.Combine(_testRootPath, "attachments", "photo-lesson-1-beach-photo.jpg.bin")));

		// Act - Load record
		var loaded = await store.LoadAsync<LessonRecord>("photo-lesson-1");
		Assert.NotNull(loaded);
		Assert.Equal("Photography", loaded.Subject);

		// Act - Delete
		await store.DeleteAsync("photo-lesson-1");

		// Assert - Both record and attachment deleted
		Assert.False(File.Exists(Path.Combine(_testRootPath, "records", "photo-lesson-1.dat")));
		Assert.False(File.Exists(Path.Combine(_testRootPath, "attachments", "photo-lesson-1-beach-photo.jpg.bin")));
	}

	[Fact]
	public async Task EndToEnd_MultipleRecords_ShouldBeIndependent()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		var lesson1 = new LessonRecord { Subject = "Science", Description = "Seagulls" };
		var lesson2 = new LessonRecord { Subject = "Math", Description = "Geometry" };
		var lesson3 = new LessonRecord { Subject = "Art", Description = "Painting" };

		// Act - Save multiple records
		await store.SaveAsync("lesson-1", lesson1);
		await store.SaveAsync("lesson-2", lesson2);
		await store.SaveAsync("lesson-3", lesson3);

		// Assert - All records saved
		Assert.True(File.Exists(Path.Combine(_testRootPath, "records", "lesson-1.dat")));
		Assert.True(File.Exists(Path.Combine(_testRootPath, "records", "lesson-2.dat")));
		Assert.True(File.Exists(Path.Combine(_testRootPath, "records", "lesson-3.dat")));

		// Act - Delete one record
		await store.DeleteAsync("lesson-2");

		// Assert - Only the deleted record is gone
		Assert.True(File.Exists(Path.Combine(_testRootPath, "records", "lesson-1.dat")));
		Assert.False(File.Exists(Path.Combine(_testRootPath, "records", "lesson-2.dat")));
		Assert.True(File.Exists(Path.Combine(_testRootPath, "records", "lesson-3.dat")));

		// Verify remaining records can still be loaded
		var loadedLesson1 = await store.LoadAsync<LessonRecord>("lesson-1");
		var loadedLesson3 = await store.LoadAsync<LessonRecord>("lesson-3");

		Assert.NotNull(loadedLesson1);
		Assert.Equal("Science", loadedLesson1.Subject);
		Assert.NotNull(loadedLesson3);
		Assert.Equal("Art", loadedLesson3.Subject);
	}

	[Fact]
	public async Task EndToEnd_UpdateExistingRecord_ShouldPreserveId()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		var originalLesson = new LessonRecord
		{
			Subject = "Science",
			Description = "Original description"
		};

		// Act - Save original
		await store.SaveAsync("lesson-1", originalLesson);

		// Modify and save again with same ID
		var updatedLesson = new LessonRecord
		{
			Subject = "Science - Updated",
			Description = "Updated description with more details"
		};
		await store.SaveAsync("lesson-1", updatedLesson);

		// Assert - Load should return updated version
		var loaded = await store.LoadAsync<LessonRecord>("lesson-1");
		Assert.NotNull(loaded);
		Assert.Equal("Science - Updated", loaded.Subject);
		Assert.Equal("Updated description with more details", loaded.Description);

		// Verify only one file exists (not two)
		var recordFiles = Directory.GetFiles(Path.Combine(_testRootPath, "records"), "lesson-1.*");
		Assert.Single(recordFiles);
	}

	[Fact]
	public async Task AtomicWrite_ShouldNotLeaveTemporaryFiles()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		var lessonRecord = new LessonRecord
		{
			Subject = "Test",
			Description = "Testing atomic writes"
		};

		// Act
		await store.SaveAsync("test-1", lessonRecord);

		// Assert - No .tmp files should exist
		var tmpFiles = Directory.GetFiles(Path.Combine(_testRootPath, "records"), "*.tmp");
		Assert.Empty(tmpFiles);

		// Verify the .dat file exists
		Assert.True(File.Exists(Path.Combine(_testRootPath, "records", "test-1.dat")));
	}

	[Fact]
	public async Task EncryptionAtRest_DataShouldNotBeReadableFromDisk()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		var sensitiveData = new LessonRecord
		{
			Subject = "SENSITIVE_SUBJECT",
			Description = "SENSITIVE_DESCRIPTION_12345"
		};

		// Act
		await store.SaveAsync("sensitive-1", sensitiveData);

		// Assert - Read raw file content and verify sensitive data is not visible
		var filePath = Path.Combine(_testRootPath, "records", "sensitive-1.dat");
		var rawContent = await File.ReadAllTextAsync(filePath);

		Assert.DoesNotContain("SENSITIVE_SUBJECT", rawContent);
		Assert.DoesNotContain("SENSITIVE_DESCRIPTION", rawContent);
		Assert.DoesNotContain("12345", rawContent);
	}

	[Fact]
	public async Task DifferentKeys_ShouldNotDecryptEachOthersData()
	{
		// Arrange
		var key1 = new byte[32];
		var key2 = new byte[32];
		Random.Shared.NextBytes(key1);
		Random.Shared.NextBytes(key2);

		var crypto1 = new AesGcmEncryptionProvider(key1);
		var crypto2 = new AesGcmEncryptionProvider(key2);

		var store1 = new FileOfflineStore(_testRootPath, crypto1);
		var store2 = new FileOfflineStore(_testRootPath, crypto2);

		var testData = new LessonRecord { Subject = "Test", Description = "Test data" };

		// Act - Save with first key
		await store1.SaveAsync("test-1", testData);

		// Assert - Try to load with second key should fail
		// AuthenticationTagMismatchException is a subclass of CryptographicException
		await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(
			async () => await store2.LoadAsync<LessonRecord>("test-1")
		);
	}

	// Test data model matching README example
	private class LessonRecord
	{
		public string Subject { get; set; } = string.Empty;
		public DateTimeOffset Date { get; set; }
		public string Description { get; set; } = string.Empty;
		public List<string> Tags { get; set; } = new();
	}
}
