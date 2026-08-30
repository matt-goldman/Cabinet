using Cabinet.Core;
using Cabinet.Security;

namespace Cabinet.Tests;

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
	public async Task SaveAsync_WithAttachments_ShouldRoundTripContent()
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

		// Assert - the content comes back through the library, byte for byte
		await using var content = await store.OpenAttachmentAsync("test-id", "test.bin");
		Assert.NotNull(content);

		using var buffer = new MemoryStream();
		await content.CopyToAsync(buffer);
		Assert.Equal(attachmentContent, buffer.ToArray());
	}

	[Fact]
	public async Task SaveAsync_WithAttachments_ShouldEncryptContentOnDisk()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		var attachmentContent = new byte[] { 1, 2, 3, 4, 5 };
		var attachment = new FileAttachment("test.bin", "application/octet-stream", attachmentContent);

		// Act
		await store.SaveAsync("test-id", testData, new[] { attachment });

		// Assert - no plaintext anywhere under the attachments directory
		var files = Directory.GetFiles(Path.Combine(_testRootPath, "attachments"), "*", SearchOption.AllDirectories);
		Assert.NotEmpty(files);

		foreach (var file in files)
		{
			var bytes = await File.ReadAllBytesAsync(file);
			Assert.False(ContainsSequence(bytes, attachmentContent), $"Plaintext content found in {file}.");
		}
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
		Assert.Null(await store.OpenAttachmentAsync("test-id", "test.bin"));
		Assert.Empty(await store.ListAttachmentsAsync("test-id"));
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
		var listed = await store.ListAttachmentsAsync("test-id");
		Assert.Equal(3, listed.Count);
		Assert.Equal(["file1.bin", "file2.bin", "file3.bin"], listed.Select(a => a.Name).Order());

		foreach (var (name, expected) in new[] { ("file1.bin", (byte)1), ("file2.bin", (byte)2), ("file3.bin", (byte)3) })
		{
			await using var content = await store.OpenAttachmentAsync("test-id", name);
			Assert.NotNull(content);

			using var buffer = new MemoryStream();
			await content.CopyToAsync(buffer);
			Assert.Equal([expected], buffer.ToArray());
		}
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

	private static bool ContainsSequence(byte[] haystack, byte[] needle)
	{
		if (needle.Length == 0 || haystack.Length < needle.Length) return false;

		for (var i = 0; i <= haystack.Length - needle.Length; i++)
		{
			if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return true;
		}

		return false;
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
