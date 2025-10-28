using Plugin.Maui.OfflineData.Abstractions;
using Plugin.Maui.OfflineData.Core;
using Plugin.Maui.OfflineData.Extensions;
using Plugin.Maui.OfflineData.Security;

namespace Plugin.Maui.OfflineData.Tests;

/// <summary>
/// Tests for LINQ-style extension methods on IOfflineStore.
/// </summary>
public class OfflineStoreExtensionsTests : IDisposable
{
	private readonly string _testRootPath;
	private readonly byte[] _testKey;

	public OfflineStoreExtensionsTests()
	{
		_testRootPath = Path.Combine(Path.GetTempPath(), $"ExtensionsTests_{Guid.NewGuid()}");
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
	public async Task FindManyAsync_ShouldReturnDataOnly()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		await store.SaveAsync("id-1", new TestRecord { Name = "Seagulls at the beach", Value = 1 });
		await store.SaveAsync("id-2", new TestRecord { Name = "Dolphins swimming", Value = 2 });
		await store.SaveAsync("id-3", new TestRecord { Name = "Seagulls on the pier", Value = 3 });

		// Act
		var results = await store.FindManyAsync<TestRecord>("Seagulls");

		// Assert
		var resultList = results.ToList();
		Assert.Equal(2, resultList.Count);
		Assert.All(resultList, r => Assert.Contains("Seagulls", r.Name));
	}

	[Fact]
	public async Task WhereMatch_ShouldFilterResults()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		await store.SaveAsync("id-1", new TestRecord { Name = "Seagulls", Value = 1 });
		await store.SaveAsync("id-2", new TestRecord { Name = "Seagulls", Value = 10 });
		await store.SaveAsync("id-3", new TestRecord { Name = "Seagulls", Value = 100 });

		// Act
		var records = await store.FindManyAsync<TestRecord>("Seagulls");
		var results = records.WhereMatch(r => r.Value >= 10);

		// Assert
		var resultList = results.ToList();
		Assert.Equal(2, resultList.Count);
		Assert.All(resultList, r => Assert.True(r.Value >= 10));
	}

	[Fact]
	public async Task WhereMatch_CanBeChained()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		await store.SaveAsync("id-1", new TestRecord { Name = "Seagulls at beach", Value = 1 });
		await store.SaveAsync("id-2", new TestRecord { Name = "Seagulls at pier", Value = 10 });
		await store.SaveAsync("id-3", new TestRecord { Name = "Seagulls flying", Value = 100 });

		// Act
		var records = await store.FindManyAsync<TestRecord>("Seagulls");
		var results = records
			.WhereMatch(r => r.Value >= 10)
			.WhereMatch(r => r.Name.Contains("pier"));

		// Assert
		var resultList = results.ToList();
		Assert.Single(resultList);
		Assert.Contains("pier", resultList[0].Name);
	}

	[Fact]
	public async Task FindWhereAsync_ShouldCombineLookupAndFilter()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		await store.SaveAsync("id-1", new TestRecord { Name = "Seagulls", Value = 1 });
		await store.SaveAsync("id-2", new TestRecord { Name = "Seagulls", Value = 10 });
		await store.SaveAsync("id-3", new TestRecord { Name = "Dolphins", Value = 10 });

		// Act
		var results = await store.FindWhereAsync<TestRecord>(
			r => r.Value == 10,
			"Seagulls", "Dolphins");

		// Assert
		var resultList = results.ToList();
		Assert.Equal(2, resultList.Count);
		Assert.All(resultList, r => Assert.Equal(10, r.Value));
	}

	[Fact]
	public async Task FindManyAsync_WithMultipleTerms_ShouldPerformOrSearch()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		await store.SaveAsync("id-1", new TestRecord { Name = "Maths lesson", Value = 1 });
		await store.SaveAsync("id-2", new TestRecord { Name = "Science lesson", Value = 2 });
		await store.SaveAsync("id-3", new TestRecord { Name = "English lesson", Value = 3 });

		// Act
		var results = await store.FindManyAsync<TestRecord>("Maths", "Science");

		// Assert
		var resultList = results.ToList();
		Assert.Equal(2, resultList.Count);
		Assert.Contains(resultList, r => r.Name.Contains("Maths"));
		Assert.Contains(resultList, r => r.Name.Contains("Science"));
	}

	[Fact]
	public async Task LinqExtensions_RealWorldExample()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var mockIndexer = new MockIndexProvider();
		var store = new FileOfflineStore(_testRootPath, crypto, mockIndexer);

		// Simulate lesson records
		await store.SaveAsync("lesson-1", new LessonRecord 
		{ 
			Subject = "Maths", 
			Description = "Counting seagulls at the beach",
			Children = new List<string> { "Dylan", "Alice" },
			Date = new DateOnly(2025, 10, 27)
		});
		
		await store.SaveAsync("lesson-2", new LessonRecord 
		{ 
			Subject = "Science", 
			Description = "Building a volcano with Jessica",
			Children = new List<string> { "Jessica" },
			Date = new DateOnly(2025, 10, 28)
		});
		
		await store.SaveAsync("lesson-3", new LessonRecord 
		{ 
			Subject = "Maths", 
			Description = "Dylan practiced multiplication with seagulls",
			Children = new List<string> { "Dylan" },
			Date = new DateOnly(2025, 10, 29)
		});

		// Act - Find all lessons about seagulls where Dylan participated in Maths
		var records = await store.FindManyAsync<LessonRecord>("seagulls", "Dylan");
		var results = records
			.WhereMatch(r => r.Subject == "Maths")
			.WhereMatch(r => r.Children.Contains("Dylan"))
			.OrderBy(r => r.Date);

		// Assert
		var resultList = results.ToList();
		Assert.Equal(2, resultList.Count);
		Assert.All(resultList, r => 
		{
			Assert.Equal("Maths", r.Subject);
			Assert.Contains("Dylan", r.Children);
		});
	}

	// Test models
	private class TestRecord
	{
		public string Name { get; set; } = string.Empty;
		public int Value { get; set; }
	}

	private class LessonRecord
	{
		public string Subject { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public List<string> Children { get; set; } = new();
		public DateOnly Date { get; set; }
	}

	// Mock index provider
	private class MockIndexProvider : IIndexProvider
	{
		private readonly Dictionary<string, string> _index = new();

		public Task IndexAsync(string id, string content, IDictionary<string, string> metadata)
		{
			_index[id] = content.ToLowerInvariant();
			return Task.CompletedTask;
		}

		public Task<IEnumerable<SearchResult>> QueryAsync(string query)
		{
			var lowerQuery = query.ToLowerInvariant();
			var results = new List<SearchResult>();

			foreach (var (id, content) in _index)
			{
				if (content.Contains(lowerQuery))
				{
					var score = 1.0 / (results.Count + 1);
					var header = new RecordHeader(id, DateTimeOffset.UtcNow);
					results.Add(new SearchResult(id, score, header));
				}
			}

			return Task.FromResult<IEnumerable<SearchResult>>(results);
		}

		public Task ClearAsync()
		{
			_index.Clear();
			return Task.CompletedTask;
		}
	}
}
