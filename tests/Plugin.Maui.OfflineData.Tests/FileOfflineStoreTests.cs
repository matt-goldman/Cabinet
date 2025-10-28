using Plugin.Maui.OfflineData.Core;
using Plugin.Maui.OfflineData.Security;

namespace Plugin.Maui.OfflineData.Tests;

public class FileOfflineStoreTests : IDisposable
{
	private readonly string _testRootPath;
	private readonly byte[] _testKey;

	public FileOfflineStoreTests()
	{
		_testRootPath = Path.Combine(Path.GetTempPath(), $"OfflineStoreTests_{Guid.NewGuid()}");
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
	public void Constructor_ShouldCreateRequiredDirectories()
	{
		// Arrange & Act
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		// Assert
		Assert.True(Directory.Exists(Path.Combine(_testRootPath, "records")));
		Assert.True(Directory.Exists(Path.Combine(_testRootPath, "attachments")));
		Assert.True(Directory.Exists(Path.Combine(_testRootPath, "index")));
	}

	[Fact]
	public async Task SaveAsync_ShouldCreateEncryptedFile()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		// Act
		await store.SaveAsync("test-id", testData);

		// Assert
		var filePath = Path.Combine(_testRootPath, "records", "test-id.dat");
		Assert.True(File.Exists(filePath));

		// Verify file is encrypted (not readable as plain JSON)
		var fileContent = await File.ReadAllTextAsync(filePath);
		Assert.DoesNotContain("Test", fileContent);
	}

	[Fact]
	public async Task LoadAsync_ShouldReturnSavedData()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		// Act
		await store.SaveAsync("test-id", testData);
		var loaded = await store.LoadAsync<TestRecord>("test-id");

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(testData.Name, loaded.Name);
		Assert.Equal(testData.Value, loaded.Value);
	}

	[Fact]
	public async Task LoadAsync_WithNonExistentId_ShouldReturnNull()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		// Act
		var loaded = await store.LoadAsync<TestRecord>("non-existent-id");

		// Assert
		Assert.Null(loaded);
	}

	[Fact]
	public async Task SaveAsync_ShouldOverwriteExistingData()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var originalData = new TestRecord { Name = "Original", Value = 1 };
		var updatedData = new TestRecord { Name = "Updated", Value = 2 };

		// Act
		await store.SaveAsync("test-id", originalData);
		await store.SaveAsync("test-id", updatedData);
		var loaded = await store.LoadAsync<TestRecord>("test-id");

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(updatedData.Name, loaded.Name);
		Assert.Equal(updatedData.Value, loaded.Value);
	}

	[Fact]
	public async Task DeleteAsync_ShouldRemoveFile()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		// Act
		await store.SaveAsync("test-id", testData);
		await store.DeleteAsync("test-id");

		// Assert
		var filePath = Path.Combine(_testRootPath, "records", "test-id.dat");
		Assert.False(File.Exists(filePath));
	}

	[Fact]
	public async Task DeleteAsync_WithNonExistentId_ShouldNotThrow()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);

		// Act & Assert - should not throw
		await store.DeleteAsync("non-existent-id");
	}

	[Fact]
	public async Task SaveAsync_WithAttachments_ShouldSaveAttachmentFiles()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		var attachmentContent = new byte[] { 1, 2, 3, 4, 5 };
		var attachmentStream = new MemoryStream(attachmentContent);
		var attachment = new FileAttachment("test.bin", "application/octet-stream", attachmentStream);

		// Act
		await store.SaveAsync("test-id", testData, new[] { attachment });

		// Assert
		var attachmentPath = Path.Combine(_testRootPath, "attachments", "test-id-test.bin.bin");
		Assert.True(File.Exists(attachmentPath));

		// Verify attachment is encrypted
		var attachmentBytes = await File.ReadAllBytesAsync(attachmentPath);
		Assert.NotEqual(attachmentContent, attachmentBytes);
	}

	[Fact]
	public async Task DeleteAsync_WithAttachments_ShouldDeleteAttachments()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		var attachmentStream = new MemoryStream(new byte[] { 1, 2, 3 });
		var attachment = new FileAttachment("test.bin", "application/octet-stream", attachmentStream);

		// Act
		await store.SaveAsync("test-id", testData, new[] { attachment });
		await store.DeleteAsync("test-id");

		// Assert
		var attachmentPath = Path.Combine(_testRootPath, "attachments", "test-id-test.bin.bin");
		Assert.False(File.Exists(attachmentPath));
	}

	[Fact]
	public async Task SaveAsync_WithMultipleAttachments_ShouldSaveAllAttachments()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		var attachment1 = new FileAttachment("file1.bin", "application/octet-stream", new MemoryStream(new byte[] { 1 }));
		var attachment2 = new FileAttachment("file2.bin", "application/octet-stream", new MemoryStream(new byte[] { 2 }));
		var attachment3 = new FileAttachment("file3.bin", "application/octet-stream", new MemoryStream(new byte[] { 3 }));

		// Act
		await store.SaveAsync("test-id", testData, new[] { attachment1, attachment2, attachment3 });

		// Assert
		var attachmentPath1 = Path.Combine(_testRootPath, "attachments", "test-id-file1.bin.bin");
		var attachmentPath2 = Path.Combine(_testRootPath, "attachments", "test-id-file2.bin.bin");
		var attachmentPath3 = Path.Combine(_testRootPath, "attachments", "test-id-file3.bin.bin");

		Assert.True(File.Exists(attachmentPath1));
		Assert.True(File.Exists(attachmentPath2));
		Assert.True(File.Exists(attachmentPath3));
	}

	[Fact]
	public async Task FindAsync_WithoutIndexProvider_ShouldReturnEmptyResults()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto, indexer: null);

		// Act
		var results = await store.FindAsync("test query");

		// Assert
		Assert.NotNull(results);
		Assert.Empty(results);
	}

	[Fact]
	public async Task SaveAsync_WithComplexObject_ShouldPreserveStructure()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new ComplexTestRecord
		{
			Id = "complex-1",
			Timestamp = DateTimeOffset.UtcNow,
			Tags = new List<string> { "tag1", "tag2", "tag3" },
			Metadata = new Dictionary<string, string>
			{
				["key1"] = "value1",
				["key2"] = "value2"
			},
			Nested = new NestedRecord
			{
				Description = "Nested data",
				Count = 100
			}
		};

		// Act
		await store.SaveAsync("complex-id", testData);
		var loaded = await store.LoadAsync<ComplexTestRecord>("complex-id");

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(testData.Id, loaded.Id);
		Assert.Equal(testData.Tags, loaded.Tags);
		Assert.Equal(testData.Metadata, loaded.Metadata);
		Assert.Equal(testData.Nested.Description, loaded.Nested.Description);
		Assert.Equal(testData.Nested.Count, loaded.Nested.Count);
	}

	// Test data models
	private class TestRecord
	{
		public string Name { get; set; } = string.Empty;
		public int Value { get; set; }
	}

	private class ComplexTestRecord
	{
		public string Id { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; set; }
		public List<string> Tags { get; set; } = new();
		public Dictionary<string, string> Metadata { get; set; } = new();
		public NestedRecord Nested { get; set; } = new();
	}

	private class NestedRecord
	{
		public string Description { get; set; } = string.Empty;
		public int Count { get; set; }
	}
}
