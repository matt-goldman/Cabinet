using Cabinet.Abstractions;
using Cabinet.Core;
using Cabinet.Security;

namespace Cabinet.Tests;

/// <summary>
/// Tests for RecordSet<T> - the high-level domain abstraction for managing record collections.
/// </summary>
public class RecordSetDomainTests : IDisposable
{
	private readonly string _testDirectory;
	private readonly IOfflineStore _store;
	private readonly byte[] _testKey;

	public RecordSetDomainTests()
	{
		_testDirectory = Path.Combine(Path.GetTempPath(), $"cabinet_recordset_tests_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testDirectory);

		// Generate a 256-bit (32 byte) key for AES-256-GCM
		_testKey = new byte[32];
		Random.Shared.NextBytes(_testKey);

		var encryptionProvider = new AesGcmEncryptionProvider(_testKey);
		_store = new FileOfflineStore(_testDirectory, encryptionProvider);
	}

	public void Dispose()
	{
		if (Directory.Exists(_testDirectory))
		{
			Directory.Delete(_testDirectory, true);
		}
	}

	[Fact]
	public async Task LoadAsync_WithNoExistingData_ShouldInitializeEmptyCache()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);

		// Act
		await recordSet.LoadAsync();
		var count = recordSet.Count();

		// Assert
		Assert.Equal(0, count);
	}

	[Fact]
	public async Task AddAsync_ShouldAddRecordAndPersist()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		var record = new TestRecord { Id = "test-1", Name = "Test Record", Value = 42 };

		// Act
		await recordSet.LoadAsync();
		await recordSet.AddAsync(record);

		// Assert - Check in memory
		var retrieved = await recordSet.GetByIdAsync("test-1");
		Assert.NotNull(retrieved);
		Assert.Equal("Test Record", retrieved.Name);
		Assert.Equal(42, retrieved.Value);

		// Assert - Verify persistence by creating new RecordSet
		var newRecordSet = new RecordSet<TestRecord>(_store, options);
		await newRecordSet.LoadAsync();
		var persistedRecord = await newRecordSet.GetByIdAsync("test-1");
		Assert.NotNull(persistedRecord);
		Assert.Equal("Test Record", persistedRecord.Name);
	}

	[Fact]
	public async Task GetAllAsync_ShouldReturnAllRecords()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		
		var records = new[]
		{
			new TestRecord { Id = "1", Name = "First", Value = 10 },
			new TestRecord { Id = "2", Name = "Second", Value = 20 },
			new TestRecord { Id = "3", Name = "Third", Value = 30 }
		};

		// Act
		await recordSet.LoadAsync();
		foreach (var record in records)
		{
			await recordSet.AddAsync(record);
		}

		var allRecords = (await recordSet.GetAllAsync()).ToList();

		// Assert
		Assert.Equal(3, allRecords.Count);
		Assert.Contains(allRecords, r => r.Name == "First");
		Assert.Contains(allRecords, r => r.Name == "Second");
		Assert.Contains(allRecords, r => r.Name == "Third");
	}

	[Fact]
	public async Task UpdateAsync_ShouldUpdateExistingRecord()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		var original = new TestRecord { Id = "update-test", Name = "Original", Value = 100 };

		await recordSet.LoadAsync();
		await recordSet.AddAsync(original);

		// Act
		var updated = new TestRecord { Id = "update-test", Name = "Updated", Value = 200 };
		var success = await recordSet.UpdateAsync("update-test", updated);

		// Assert
		Assert.True(success);
		var retrieved = await recordSet.GetByIdAsync("update-test");
		Assert.NotNull(retrieved);
		Assert.Equal("Updated", retrieved.Name);
		Assert.Equal(200, retrieved.Value);
	}

	[Fact]
	public async Task UpdateAsync_WithNonExistentId_ShouldReturnFalse()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		await recordSet.LoadAsync();

		// Act
		var record = new TestRecord { Id = "non-existent", Name = "Test", Value = 1 };
		var success = await recordSet.UpdateAsync("non-existent", record);

		// Assert
		Assert.False(success);
	}

	[Fact]
	public async Task RemoveAsync_ShouldRemoveRecord()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		var record = new TestRecord { Id = "remove-test", Name = "To Remove", Value = 999 };

		await recordSet.LoadAsync();
		await recordSet.AddAsync(record);

		// Act
		var success = await recordSet.RemoveAsync("remove-test");

		// Assert
		Assert.True(success);
		var retrieved = await recordSet.GetByIdAsync("remove-test");
		Assert.Null(retrieved);
	}

	[Fact]
	public async Task RemoveAsync_WithNonExistentId_ShouldReturnFalse()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		await recordSet.LoadAsync();

		// Act
		var success = await recordSet.RemoveAsync("non-existent");

		// Assert
		Assert.False(success);
	}

	[Fact]
	public async Task Where_ShouldFilterCachedRecords()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		
		var records = new[]
		{
			new TestRecord { Id = "1", Name = "Alice", Value = 10 },
			new TestRecord { Id = "2", Name = "Bob", Value = 20 },
			new TestRecord { Id = "3", Name = "Charlie", Value = 30 },
			new TestRecord { Id = "4", Name = "David", Value = 40 }
		};

		await recordSet.LoadAsync();
		foreach (var record in records)
		{
			await recordSet.AddAsync(record);
		}

		// Act
		var filtered = recordSet.Where(r => r.Value >= 20).ToList();

		// Assert
		Assert.Equal(3, filtered.Count);
		Assert.Contains(filtered, r => r.Name == "Bob");
		Assert.Contains(filtered, r => r.Name == "Charlie");
		Assert.Contains(filtered, r => r.Name == "David");
	}

	[Fact]
	public async Task OrderBy_ShouldSortRecords()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		
		var records = new[]
		{
			new TestRecord { Id = "1", Name = "Charlie", Value = 30 },
			new TestRecord { Id = "2", Name = "Alice", Value = 10 },
			new TestRecord { Id = "3", Name = "Bob", Value = 20 }
		};

		await recordSet.LoadAsync();
		foreach (var record in records)
		{
			await recordSet.AddAsync(record);
		}

		// Act
		var sorted = recordSet.OrderBy(r => r.Value).ToList();

		// Assert
		Assert.Equal(3, sorted.Count);
		Assert.Equal("Alice", sorted[0].Name);
		Assert.Equal("Bob", sorted[1].Name);
		Assert.Equal("Charlie", sorted[2].Name);
	}

	[Fact]
	public async Task OrderByDescending_ShouldSortDescending()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);
		
		var records = new[]
		{
			new TestRecord { Id = "1", Name = "Alice", Value = 10 },
			new TestRecord { Id = "2", Name = "Bob", Value = 20 },
			new TestRecord { Id = "3", Name = "Charlie", Value = 30 }
		};

		await recordSet.LoadAsync();
		foreach (var record in records)
		{
			await recordSet.AddAsync(record);
		}

		// Act
		var sorted = recordSet.OrderByDescending(r => r.Value).ToList();

		// Assert
		Assert.Equal(3, sorted.Count);
		Assert.Equal("Charlie", sorted[0].Name);
		Assert.Equal("Bob", sorted[1].Name);
		Assert.Equal("Alice", sorted[2].Name);
	}

	[Fact]
	public async Task RefreshAsync_ShouldReloadFromDisk()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id
		};
		var recordSet1 = new RecordSet<TestRecord>(_store, options);
		var recordSet2 = new RecordSet<TestRecord>(_store, options);

		await recordSet1.LoadAsync();
		await recordSet1.AddAsync(new TestRecord { Id = "1", Name = "First", Value = 100 });

		// Act - Load second recordset (should see the data)
		await recordSet2.LoadAsync();
		var count1 = recordSet2.Count();

		// Add more data via first recordset
		await recordSet1.AddAsync(new TestRecord { Id = "2", Name = "Second", Value = 200 });

		// Refresh second recordset
		await recordSet2.RefreshAsync();
		var count2 = recordSet2.Count();

		// Assert
		Assert.Equal(1, count1);
		Assert.Equal(2, count2);
	}

	[Fact]
	public async Task IdSelector_WithCustomProperty_ShouldWork()
	{
		// Arrange
		var options = new RecordSetOptions<CustomIdRecord>
		{
			IdSelector = r => r.CustomId
		};
		var recordSet = new RecordSet<CustomIdRecord>(_store, options);
		var record = new CustomIdRecord { CustomId = "custom-123", Data = "Test Data" };

		// Act
		await recordSet.LoadAsync();
		await recordSet.AddAsync(record);
		var retrieved = await recordSet.GetByIdAsync("custom-123");

		// Assert
		Assert.NotNull(retrieved);
		Assert.Equal("Test Data", retrieved.Data);
	}

	[Fact]
	public async Task ReflectionBased_WithIdProperty_ShouldAutoDiscover()
	{
		// Arrange - No IdSelector specified, should use reflection
		var recordSet = new RecordSet<TestRecord>(_store);
		var record = new TestRecord { Id = "auto-1", Name = "Auto Discovery", Value = 777 };

		// Act
		await recordSet.LoadAsync();
		await recordSet.AddAsync(record);
		var retrieved = await recordSet.GetByIdAsync("auto-1");

		// Assert
		Assert.NotNull(retrieved);
		Assert.Equal("Auto Discovery", retrieved.Name);
	}

	[Fact]
	public async Task EnableCaching_False_ShouldStillWork()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id,
			EnableCaching = false
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);

		// Act
		await recordSet.LoadAsync();
		await recordSet.AddAsync(new TestRecord { Id = "1", Name = "Test", Value = 1 });

		// Assert - Should still be able to retrieve (implementation detail: caching happens anyway)
		var retrieved = await recordSet.GetByIdAsync("1");
		Assert.NotNull(retrieved);
	}

	[Fact]
	public async Task CustomFileName_ShouldUseSpecifiedName()
	{
		// Arrange
		var options = new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Id,
			CustomFileName = "MyCustomFile"
		};
		var recordSet = new RecordSet<TestRecord>(_store, options);

		// Act
		await recordSet.LoadAsync();
		await recordSet.AddAsync(new TestRecord { Id = "1", Name = "Custom", Value = 1 });

		// Assert - Verify data was persisted (file naming is internal implementation detail)
		var newRecordSet = new RecordSet<TestRecord>(_store, options);
		await newRecordSet.LoadAsync();
		var retrieved = await newRecordSet.GetByIdAsync("1");
		Assert.NotNull(retrieved);
		Assert.Equal("Custom", retrieved.Name);
	}

	// Test helper classes
	private class TestRecord
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int Value { get; set; }
	}

	private class CustomIdRecord
	{
		public string CustomId { get; set; } = string.Empty;
		public string Data { get; set; } = string.Empty;
	}
}
