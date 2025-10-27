using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Plugin.Maui.OfflineData.Core;
using Plugin.Maui.OfflineData.Index;
using Plugin.Maui.OfflineData.Security;

namespace Plugin.Maui.OfflineData.Benchmarks;

/// <summary>
/// Benchmarks for PersistentIndexProvider with various dataset sizes.
/// Tests write, read, search, and persistence operations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class PersistentIndexBenchmarks
{
	private string _testPath = string.Empty;
	private byte[] _encryptionKey = Array.Empty<byte>();
	private AesGcmEncryptionProvider _encryptionProvider = null!;
	private FileOfflineStore _store = null!;

	[Params(10, 100, 1000)]
	public int RecordCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_testPath = Path.Combine(Path.GetTempPath(), $"Benchmarks_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testPath);

		_encryptionKey = new byte[32];
		Random.Shared.NextBytes(_encryptionKey);
		_encryptionProvider = new AesGcmEncryptionProvider(_encryptionKey);

		var indexProvider = new PersistentIndexProvider(_testPath, _encryptionProvider);
		_store = new FileOfflineStore(_testPath, _encryptionProvider, indexProvider);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_testPath))
		{
			Directory.Delete(_testPath, recursive: true);
		}
	}

	[Benchmark(Description = "Save and Index Records")]
	public async Task SaveRecordsWithIndexing()
	{
		for (int i = 0; i < RecordCount; i++)
		{
			var record = new TestRecord
			{
				Id = $"record-{i}",
				Title = $"Test Record {i}",
				Description = $"This is test record number {i} with some searchable content about seagulls and beaches",
				Category = i % 3 == 0 ? "Science" : i % 3 == 1 ? "Math" : "History",
				Timestamp = DateTimeOffset.UtcNow
			};

			await _store.SaveAsync($"record-{i}", record);
		}
	}

	[Benchmark(Description = "Search Single Term")]
	public async Task SearchSingleTerm()
	{
		// First populate if empty
		await EnsureDataPopulated();
		
		// Search for common term
		var results = await _store.SearchAsync("seagulls");
		_ = results.ToList(); // Materialize results
	}

	[Benchmark(Description = "Search Multiple Terms")]
	public async Task SearchMultipleTerms()
	{
		// First populate if empty
		await EnsureDataPopulated();
		
		// Search with multiple terms
		var results = await _store.SearchAsync("seagulls beaches content");
		_ = results.ToList(); // Materialize results
	}

	[Benchmark(Description = "Load Single Record")]
	public async Task LoadSingleRecord()
	{
		// First populate if empty
		await EnsureDataPopulated();
		
		// Load a record from the middle
		var record = await _store.LoadAsync<TestRecord>($"record-{RecordCount / 2}");
	}

	[Benchmark(Description = "Update Existing Record")]
	public async Task UpdateExistingRecord()
	{
		// First populate if empty
		await EnsureDataPopulated();
		
		// Update a record
		var record = new TestRecord
		{
			Id = "record-updated",
			Title = "Updated Record",
			Description = "This record has been updated with new content",
			Category = "Updated",
			Timestamp = DateTimeOffset.UtcNow
		};

		await _store.SaveAsync("record-0", record);
	}

	private async Task EnsureDataPopulated()
	{
		// Check if data exists, if not populate it
		try
		{
			var test = await _store.LoadAsync<TestRecord>("record-0");
			if (test == null)
			{
				await SaveRecordsWithIndexing();
			}
		}
		catch
		{
			await SaveRecordsWithIndexing();
		}
	}

	private class TestRecord
	{
		public string Id { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string Category { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; set; }
	}
}

/// <summary>
/// Benchmarks specifically for index operations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class IndexOperationBenchmarks
{
	private string _testPath = string.Empty;
	private byte[] _encryptionKey = Array.Empty<byte>();
	private AesGcmEncryptionProvider _encryptionProvider = null!;
	private PersistentIndexProvider _indexProvider = null!;

	[Params(10, 100, 1000)]
	public int EntryCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_testPath = Path.Combine(Path.GetTempPath(), $"IndexBenchmarks_{Guid.NewGuid()}");
		Directory.CreateDirectory(_testPath);

		_encryptionKey = new byte[32];
		Random.Shared.NextBytes(_encryptionKey);
		_encryptionProvider = new AesGcmEncryptionProvider(_encryptionKey);
		_indexProvider = new PersistentIndexProvider(_testPath, _encryptionProvider);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_testPath))
		{
			Directory.Delete(_testPath, recursive: true);
		}
	}

	[Benchmark(Description = "Index Entries")]
	public async Task IndexEntries()
	{
		for (int i = 0; i < EntryCount; i++)
		{
			var content = $"Entry {i}: Seagulls flying over the beach at sunset on day {i}";
			var metadata = new Dictionary<string, string>
			{
				{ "index", i.ToString() },
				{ "category", (i % 3).ToString() }
			};
			await _indexProvider.IndexAsync($"entry-{i}", content, metadata);
		}
	}

	[Benchmark(Description = "Query Index")]
	public async Task QueryIndex()
	{
		// First populate if empty
		await EnsureIndexPopulated();
		
		var results = await _indexProvider.QueryAsync("seagulls beach");
		_ = results.ToList(); // Materialize results
	}

	[Benchmark(Description = "Clear Index")]
	public async Task ClearIndex()
	{
		// First populate
		await EnsureIndexPopulated();
		
		await _indexProvider.ClearAsync();
	}

	[Benchmark(Description = "Reload Index (Cold Start)")]
	public async Task ReloadIndex()
	{
		// First populate and save
		await EnsureIndexPopulated();
		
		// Create new instance to simulate cold start
		var newIndexProvider = new PersistentIndexProvider(_testPath, _encryptionProvider);
		var results = await newIndexProvider.QueryAsync("seagulls");
		_ = results.ToList();
	}

	private async Task EnsureIndexPopulated()
	{
		var results = await _indexProvider.QueryAsync("seagulls");
		if (!results.Any())
		{
			await IndexEntries();
		}
	}
}
