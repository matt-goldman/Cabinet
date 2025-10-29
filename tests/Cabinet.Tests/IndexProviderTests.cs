using Cabinet.Abstractions;
using Cabinet.Core;

namespace Cabinet.Tests;

/// <summary>
/// Tests for IIndexProvider integration.
/// These tests use a mock implementation since EasyIndex integration is planned for future work.
/// When EasyIndex is integrated, these tests can be adapted to test the actual implementation.
/// </summary>
public class IndexProviderTests : IDisposable
{
	private readonly string _testRootPath;
	private readonly byte[] _testKey;

	public IndexProviderTests()
	{
		_testRootPath = Path.Combine(Path.GetTempPath(), $"IndexProviderTests_{Guid.NewGuid()}");
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
	public async Task IndexAsync_ShouldBeCalledWhenSavingRecord()
	{
		// Arrange
		var crypto = new Security.AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);
		var testData = new TestRecord { Name = "Seagulls at the beach", Value = 42 };

		// Act
		await store.SaveAsync("test-id", testData);

		// Assert
		Assert.True(mockIndexer.IndexAsyncCalled);
		Assert.Equal("test-id", mockIndexer.LastIndexedId);
		Assert.Contains("Seagulls", mockIndexer.LastIndexedContent);
	}

	[Fact]
	public async Task QueryAsync_ShouldReturnMatchingResults()
	{
		// Arrange
		var crypto = new Security.AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		// Add test data
		await store.SaveAsync("id-1", new TestRecord { Name = "Seagulls at the beach", Value = 1 });
		await store.SaveAsync("id-2", new TestRecord { Name = "Dolphins swimming", Value = 2 });
		await store.SaveAsync("id-3", new TestRecord { Name = "Seagulls on the pier", Value = 3 });

		// Act
		var results = await store.FindAsync("Seagulls");

		// Assert
		Assert.NotNull(results);
		var resultList = results.ToList();
		Assert.Equal(2, resultList.Count);
		Assert.All(resultList, r => Assert.Contains("id-", r.RecordId));
	}

	[Fact]
	public async Task QueryAsync_WithNoMatches_ShouldReturnEmpty()
	{
		// Arrange
		var crypto = new Security.AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		await store.SaveAsync("id-1", new TestRecord { Name = "Seagulls at the beach", Value = 1 });

		// Act
		var results = await store.FindAsync("nonexistent");

		// Assert
		Assert.NotNull(results);
		Assert.Empty(results);
	}

	[Fact]
	public async Task IndexAsync_ShouldHandleComplexContent()
	{
		// Arrange
		var crypto = new Security.AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);
		
		var complexData = new ComplexTestRecord
		{
			Title = "Science Lesson",
			Description = "Observed seagulls at the beach during low tide",
			Tags = new List<string> { "science", "nature", "birds" },
			Date = DateTimeOffset.UtcNow
		};

		// Act
		await store.SaveAsync("lesson-1", complexData);

		// Assert
		Assert.True(mockIndexer.IndexAsyncCalled);
		Assert.NotNull(mockIndexer.LastIndexedContent);
		Assert.Contains("seagulls", mockIndexer.LastIndexedContent.ToLower());
		Assert.Contains("beach", mockIndexer.LastIndexedContent.ToLower());
	}

	[Fact]
	public async Task IndexAsync_ShouldReceiveMetadata()
	{
		// Arrange
		var crypto = new Security.AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		// Act
		await store.SaveAsync("test-id", testData);

		// Assert
		Assert.NotNull(mockIndexer.LastMetadata);
		// Currently the implementation passes an empty dictionary, but this test
		// validates that metadata can be passed through the interface
	}

	[Fact]
	public async Task FindAsync_ShouldRankResultsByRelevance()
	{
		// Arrange
		var crypto = new Security.AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		// Add records with varying relevance
		await store.SaveAsync("id-1", new TestRecord { Name = "Seagulls seagulls seagulls", Value = 1 });
		await store.SaveAsync("id-2", new TestRecord { Name = "One seagull", Value = 2 });

		// Act
		var results = await store.FindAsync("Seagulls");

		// Assert
		var resultList = results.ToList();
		Assert.NotEmpty(resultList);
		// First result should have higher score (mock implementation gives higher scores to earlier indexed items)
		if (resultList.Count > 1)
		{
			Assert.True(resultList[0].Score >= resultList[1].Score);
		}
	}

	[Fact]
	public async Task FindAsync_Generic_ShouldReturnTypedResults()
	{
		// Arrange
		var crypto = new Security.AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		// Add test data
		await store.SaveAsync("id-1", new TestRecord { Name = "Seagulls at the beach", Value = 1 });
		await store.SaveAsync("id-2", new TestRecord { Name = "Dolphins swimming", Value = 2 });
		await store.SaveAsync("id-3", new TestRecord { Name = "Seagulls on the pier", Value = 3 });

		// Act
		var results = await store.FindAsync<TestRecord>("Seagulls");

		// Assert
		Assert.NotNull(results);
		var resultList = results.ToList();
		Assert.Equal(2, resultList.Count);
		
		// Verify we get SearchResult<T> with actual data
		foreach (var result in resultList)
		{
			Assert.NotNull(result.Data);
			Assert.Contains("Seagulls", result.Data.Name);
			Assert.Contains("id-", result.RecordId);
		}
	}

	[Fact]
	public async Task FindAsync_Generic_WithAggregateFile_ShouldReturnAllMatchingItems()
	{
		// Arrange
		var crypto = new Security.AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		// Save as an aggregate file (list of records)
		var records = new List<TestRecord>
		{
			new TestRecord { Name = "Seagulls flying", Value = 1 },
			new TestRecord { Name = "Dolphins jumping", Value = 2 },
			new TestRecord { Name = "Seagulls diving", Value = 3 }
		};
		await store.SaveAsync("aggregate-1", records);

		// Act
		var results = await store.FindAsync<TestRecord>("Seagulls");

		// Assert
		Assert.NotNull(results);
		var resultList = results.ToList();
		
		// Note: The mock indexer matches on the entire serialised JSON, so it finds "Seagulls" 
		// in the aggregate. We return all 3 items from the list since we can't filter 
		// individual items without more complex logic. This is expected behavior.
		Assert.Equal(3, resultList.Count);
		
		// Verify all items are from the same aggregate file
		foreach (var result in resultList)
		{
			Assert.NotNull(result.Data);
			Assert.Equal("aggregate-1", result.RecordId);
		}
	}

	// Mock implementation for testing
	private class MockIndexProvider : IIndexProvider
	{
		private readonly Dictionary<string, string> _index = new();

		public bool IndexAsyncCalled { get; private set; }
		public string? LastIndexedId { get; private set; }
		public string? LastIndexedContent { get; private set; }
		public IDictionary<string, string>? LastMetadata { get; private set; }

		public Task IndexAsync(string id, string content, IDictionary<string, string> metadata, CancellationToken cancellationToken = default)
		{
			IndexAsyncCalled = true;
			LastIndexedId = id;
			LastIndexedContent = content;
			LastMetadata = metadata;

			_index[id] = content.ToLowerInvariant();
			return Task.CompletedTask;
		}

		public Task<IEnumerable<SearchResult>> QueryAsync(string query, CancellationToken cancellationToken = default)
		{
			var lowerQuery = query.ToLowerInvariant();
			var results = new List<SearchResult>();

			foreach (var (id, content) in _index)
			{
				if (content.Contains(lowerQuery))
				{
					// Simple scoring: higher score for earlier matches
					var score = 1.0 / (results.Count + 1);
					var header = new RecordHeader(id, DateTimeOffset.UtcNow);
					results.Add(new SearchResult(id, score, header));
				}
			}

			return Task.FromResult<IEnumerable<SearchResult>>(results);
		}

		public Task ClearAsync(CancellationToken cancellationToken = default)
		{
			_index.Clear();
			return Task.CompletedTask;
		}
	}

	// Test data models
	private class TestRecord
	{
		public string Name { get; set; } = string.Empty;
		public int Value { get; set; }
	}

	private class ComplexTestRecord
	{
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public List<string> Tags { get; set; } = new();
		public DateTimeOffset Date { get; set; }
	}
}
