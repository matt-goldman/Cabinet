using Plugin.Maui.OfflineData.Abstractions;
using Plugin.Maui.OfflineData.Index;
using Plugin.Maui.OfflineData.Security;

namespace Plugin.Maui.OfflineData.Tests;

/// <summary>
/// Tests for PersistentIndexProvider to verify encrypted persistent indexing functionality.
/// </summary>
public class PersistentIndexProviderTests : IDisposable
{
	private readonly string _testRootPath;
	private readonly byte[] _testKey;
	private readonly IEncryptionProvider _encryptionProvider;

	public PersistentIndexProviderTests()
	{
		_testRootPath = Path.Combine(Path.GetTempPath(), $"PersistentIndexProviderTests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testRootPath);

		_testKey = new byte[32];
		Random.Shared.NextBytes(_testKey);
		_encryptionProvider = new AesGcmEncryptionProvider(_testKey);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testRootPath))
		{
			Directory.Delete(_testRootPath, recursive: true);
		}
	}

	[Fact]
	public async Task IndexAsync_ShouldStoreAndPersistEntry()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		var testContent = "Seagulls at the beach";
		var metadata = new Dictionary<string, string> { { "type", "lesson" } };

		// Act
		await indexProvider.IndexAsync("test-id", testContent, metadata);

		// Assert - verify index file was created
		var indexFilePath = Path.Combine(_testRootPath, "index", "search-index.dat");
		Assert.True(File.Exists(indexFilePath));
	}

	[Fact]
	public async Task QueryAsync_ShouldReturnMatchingResults()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "Seagulls at the beach", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-2", "Dolphins swimming", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-3", "Seagulls on the pier", new Dictionary<string, string>());

		// Act
		var results = await indexProvider.QueryAsync("seagulls");

		// Assert
		var resultList = results.ToList();
		Assert.Equal(2, resultList.Count);
		Assert.All(resultList, r => Assert.Contains("id-", r.RecordId));
	}

	[Fact]
	public async Task QueryAsync_WithMultipleTerms_ShouldReturnRelevantResults()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "Seagulls at the beach during sunset", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-2", "Beach volleyball game", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-3", "Seagulls flying over the ocean", new Dictionary<string, string>());

		// Act
		var results = await indexProvider.QueryAsync("seagulls beach");

		// Assert
		var resultList = results.ToList();
		Assert.NotEmpty(resultList);
		// id-1 should score highest as it contains both terms
		Assert.Equal("id-1", resultList.First().RecordId);
	}

	[Fact]
	public async Task IndexAsync_ShouldPersistAcrossInstances()
	{
		// Arrange & Act - First instance
		var indexProvider1 = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider1.IndexAsync("id-1", "Persistent data test", new Dictionary<string, string>());
		
		// Create a new instance to simulate app restart
		var indexProvider2 = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		var results = await indexProvider2.QueryAsync("persistent");

		// Assert
		var resultList = results.ToList();
		Assert.Single(resultList);
		Assert.Equal("id-1", resultList[0].RecordId);
	}

	[Fact]
	public async Task ClearAsync_ShouldRemoveAllEntries()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "Test data one", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-2", "Test data two", new Dictionary<string, string>());

		// Act
		await indexProvider.ClearAsync();
		var results = await indexProvider.QueryAsync("test");

		// Assert
		Assert.Empty(results);
	}

	[Fact]
	public async Task ClearAsync_ShouldDeleteIndexFile()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "Test data", new Dictionary<string, string>());
		var indexFilePath = Path.Combine(_testRootPath, "index", "search-index.dat");
		Assert.True(File.Exists(indexFilePath));

		// Act
		await indexProvider.ClearAsync();

		// Assert
		Assert.False(File.Exists(indexFilePath));
	}

	[Fact]
	public async Task QueryAsync_WithShortTerms_ShouldIgnoreThem()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "A seagull at the beach", new Dictionary<string, string>());

		// Act - query with short terms that should be ignored (length <= 2)
		var results = await indexProvider.QueryAsync("at to in");

		// Assert
		Assert.Empty(results);
	}

	[Fact]
	public async Task QueryAsync_ShouldRankByRelevance()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "seagull", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-2", "seagull seagull seagull", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-3", "seagull seagull", new Dictionary<string, string>());

		// Act
		var results = await indexProvider.QueryAsync("seagull");

		// Assert
		var resultList = results.ToList();
		Assert.Equal(3, resultList.Count);
		// id-2 should be first (most occurrences)
		Assert.Equal("id-2", resultList[0].RecordId);
		// id-3 should be second
		Assert.Equal("id-3", resultList[1].RecordId);
		// id-1 should be last
		Assert.Equal("id-1", resultList[2].RecordId);
	}

	[Fact]
	public async Task IndexAsync_WithMetadata_ShouldStoreAndRetrieveMetadata()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		var metadata = new Dictionary<string, string>
		{
			{ "subject", "Science" },
			{ "date", "2025-10-27" }
		};

		// Act
		await indexProvider.IndexAsync("id-1", "Test content", metadata);
		var results = await indexProvider.QueryAsync("test");

		// Assert
		var result = results.First();
		Assert.NotNull(result.Header.Metadata);
		Assert.Equal("Science", result.Header.Metadata["subject"]);
		Assert.Equal("2025-10-27", result.Header.Metadata["date"]);
	}

	[Fact]
	public async Task IndexAsync_ShouldUpdateExistingEntry()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "Original content", new Dictionary<string, string>());

		// Act - update the same ID with new content
		await indexProvider.IndexAsync("id-1", "Updated content with new words", new Dictionary<string, string>());
		var results = await indexProvider.QueryAsync("updated");

		// Assert
		var resultList = results.ToList();
		Assert.Single(resultList);
		Assert.Equal("id-1", resultList[0].RecordId);
		
		// Verify old content doesn't match
		var oldResults = await indexProvider.QueryAsync("original");
		Assert.Empty(oldResults);
	}

	[Fact]
	public async Task QueryAsync_CaseInsensitive_ShouldMatchRegardlessOfCase()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "UPPERCASE CONTENT", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-2", "lowercase content", new Dictionary<string, string>());
		await indexProvider.IndexAsync("id-3", "MixedCase Content", new Dictionary<string, string>());

		// Act
		var results = await indexProvider.QueryAsync("content");

		// Assert
		Assert.Equal(3, results.Count());
	}

	[Fact]
	public async Task IndexAsync_WithEmptyContent_ShouldNotCrash()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);

		// Act & Assert - should not throw
		await indexProvider.IndexAsync("id-1", "", new Dictionary<string, string>());
		var results = await indexProvider.QueryAsync("anything");
		Assert.Empty(results);
	}

	[Fact]
	public async Task QueryAsync_WithEmptyQuery_ShouldReturnEmpty()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider.IndexAsync("id-1", "Test content", new Dictionary<string, string>());

		// Act
		var results = await indexProvider.QueryAsync("");

		// Assert
		Assert.Empty(results);
	}

	[Fact]
	public async Task IndexAsync_ConcurrentOperations_ShouldHandleSafely()
	{
		// Arrange
		var indexProvider = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		var tasks = new List<Task>();

		// Act - perform multiple concurrent index operations
		for (int i = 0; i < 10; i++)
		{
			var id = $"id-{i}";
			var content = $"Content number {i}";
			tasks.Add(indexProvider.IndexAsync(id, content, new Dictionary<string, string>()));
		}
		await Task.WhenAll(tasks);

		// Assert - all entries should be indexed
		var results = await indexProvider.QueryAsync("content");
		Assert.Equal(10, results.Count());
	}

	[Fact]
	public async Task PersistentIndex_ShouldSurviveEncryptionRoundTrip()
	{
		// Arrange
		var indexProvider1 = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		await indexProvider1.IndexAsync("id-1", "Test encryption persistence", new Dictionary<string, string>());
		
		// Verify the file is encrypted (not plaintext)
		var indexFilePath = Path.Combine(_testRootPath, "index", "search-index.dat");
		var fileContent = await File.ReadAllTextAsync(indexFilePath);
		Assert.DoesNotContain("Test encryption persistence", fileContent);

		// Act - create new instance with same key
		var indexProvider2 = new PersistentIndexProvider(_testRootPath, _encryptionProvider);
		var results = await indexProvider2.QueryAsync("encryption");

		// Assert
		Assert.Single(results);
	}
}
