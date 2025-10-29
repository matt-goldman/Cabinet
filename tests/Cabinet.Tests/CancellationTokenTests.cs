using Cabinet.Core;
using Cabinet.Security;
using Cabinet.Index;

namespace Cabinet.Tests;

/// <summary>
/// Tests to verify that cancellation tokens are properly supported and respected
/// across all async methods in the Cabinet API.
/// </summary>
public class CancellationTokenTests : IDisposable
{
	private readonly string _testRootPath;
	private readonly byte[] _testKey;

	public CancellationTokenTests()
	{
		_testRootPath = Path.Combine(Path.GetTempPath(), $"CancellationTokenTests_{Guid.NewGuid()}");
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

	private class TestRecord
	{
		public string Name { get; set; } = string.Empty;
		public int Value { get; set; }
	}

	[Fact]
	public async Task SaveAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await store.SaveAsync("test-id", testData, cancellationToken: cts.Token));
	}

	[Fact]
	public async Task LoadAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		// Save a record first
		await store.SaveAsync("test-id", testData);

		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await store.LoadAsync<TestRecord>("test-id", cts.Token));
	}

	[Fact]
	public async Task DeleteAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await store.DeleteAsync("test-id", cts.Token));
	}

	[Fact]
	public async Task FindAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var indexer = new PersistentIndexProvider(_testRootPath, crypto);
		var store = new FileOfflineStore(_testRootPath, crypto, indexer);

		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await store.FindAsync("test", cts.Token));
	}

	[Fact]
	public async Task EncryptAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var data = new byte[] { 1, 2, 3, 4, 5 };
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await crypto.EncryptAsync(data, "context", cts.Token));
	}

	[Fact]
	public async Task DecryptAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var data = new byte[] { 1, 2, 3, 4, 5 };

		// Encrypt first
		var encrypted = await crypto.EncryptAsync(data, "context");

		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await crypto.DecryptAsync(encrypted, "context", cts.Token));
	}

	[Fact]
	public async Task IndexAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var indexer = new PersistentIndexProvider(_testRootPath, crypto);
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await indexer.IndexAsync("id", "content", new Dictionary<string, string>(), cts.Token));
	}

	[Fact]
	public async Task QueryAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var indexer = new PersistentIndexProvider(_testRootPath, crypto);
		
		// Index something first to ensure initialization
		await indexer.IndexAsync("id", "content", new Dictionary<string, string>());

		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await indexer.QueryAsync("test", cts.Token));
	}

	[Fact]
	public async Task ClearAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var indexer = new PersistentIndexProvider(_testRootPath, crypto);
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await indexer.ClearAsync(cts.Token));
	}

	[Fact]
	public async Task RecordSet_LoadAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		
		// Save some test data first so there's something to load
		var testData = new List<TestRecord> { new TestRecord { Name = "Test", Value = 42 } };
		await store.SaveAsync("TestRecord", testData);

		var recordSet = new RecordSet<TestRecord>(store, new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Name
		});

		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await recordSet.LoadAsync(cts.Token));
	}

	[Fact]
	public async Task RecordSet_AddAsync_WithCancelledToken_ThrowsOperationCancelledException()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var recordSet = new RecordSet<TestRecord>(store, new RecordSetOptions<TestRecord>
		{
			IdSelector = r => r.Name
		});

		// Load first
		await recordSet.LoadAsync();

		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
			await recordSet.AddAsync(new TestRecord { Name = "Test", Value = 42 }, cts.Token));
	}

	[Fact]
	public async Task SaveAsync_WithValidToken_CompletesSuccessfully()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };
		using var cts = new CancellationTokenSource();

		// Act
		await store.SaveAsync("test-id", testData, cancellationToken: cts.Token);

		// Assert
		var loaded = await store.LoadAsync<TestRecord>("test-id");
		Assert.NotNull(loaded);
		Assert.Equal(testData.Name, loaded.Name);
		Assert.Equal(testData.Value, loaded.Value);
	}

	[Fact]
	public async Task LoadAsync_WithValidToken_CompletesSuccessfully()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var store = new FileOfflineStore(_testRootPath, crypto);
		var testData = new TestRecord { Name = "Test", Value = 42 };

		await store.SaveAsync("test-id", testData);
		using var cts = new CancellationTokenSource();

		// Act
		var loaded = await store.LoadAsync<TestRecord>("test-id", cts.Token);

		// Assert
		Assert.NotNull(loaded);
		Assert.Equal(testData.Name, loaded.Name);
		Assert.Equal(testData.Value, loaded.Value);
	}

	[Fact]
	public async Task IndexProvider_WithValidToken_CompletesSuccessfully()
	{
		// Arrange
		var crypto = new AesGcmEncryptionProvider(_testKey);
		var indexer = new PersistentIndexProvider(_testRootPath, crypto);
		using var cts = new CancellationTokenSource();

		// Act
		await indexer.IndexAsync("id1", "test content", new Dictionary<string, string>(), cts.Token);
		var results = await indexer.QueryAsync("test", cts.Token);

		// Assert
		Assert.NotEmpty(results);
		Assert.Contains(results, r => r.RecordId == "id1");
	}
}
