using Plugin.Maui.OfflineData.Abstractions;
using Plugin.Maui.OfflineData.Core;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Plugin.Maui.OfflineData.Index;

/// <summary>
/// A persistent index provider that stores its index to encrypted files on disk.
/// The index survives app restarts and provides full-text search with metadata support.
/// </summary>
public class PersistentIndexProvider : IIndexProvider
{
	private readonly string _indexFilePath;
	private readonly IEncryptionProvider _encryptionProvider;
	private readonly ConcurrentDictionary<string, IndexEntry> _index = new();
	private readonly SemaphoreSlim _lock = new(1, 1);
	private bool _isInitialized;
	private bool _isDirty;

	private record IndexEntry(string Id, string Content, IDictionary<string, string> Metadata, DateTimeOffset Created);

	public PersistentIndexProvider(string indexDirectory, IEncryptionProvider encryptionProvider)
	{
		_indexFilePath = Path.Combine(indexDirectory, "index", "search-index.dat");
		_encryptionProvider = encryptionProvider;
		Directory.CreateDirectory(Path.Combine(indexDirectory, "index"));
	}

	private async Task EnsureInitializedAsync()
	{
		if (_isInitialized) return;

		await _lock.WaitAsync();
		try
		{
			if (_isInitialized) return;

			// Try to load existing index from disk
			if (File.Exists(_indexFilePath))
			{
				try
				{
					var encryptedData = await File.ReadAllBytesAsync(_indexFilePath);
					var decryptedData = await _encryptionProvider.DecryptAsync(encryptedData, "search-index");
					var json = System.Text.Encoding.UTF8.GetString(decryptedData);
					var entries = JsonSerializer.Deserialize<List<IndexEntry>>(json);
					
					if (entries != null)
					{
						foreach (var entry in entries)
						{
							_index[entry.Id] = entry;
						}
					}
				}
				catch
				{
					// If load fails, start with empty index
				}
			}

			_isInitialized = true;
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task IndexAsync(string id, string content, IDictionary<string, string> metadata)
	{
		await EnsureInitializedAsync();

		await _lock.WaitAsync();
		try
		{
			var entry = new IndexEntry(id, content.ToLowerInvariant(), metadata, DateTimeOffset.UtcNow);
			_index[id] = entry;
			_isDirty = true;

			// Save to disk periodically (every index operation to ensure persistence)
			await SaveIndexAsync();
		}
		finally
		{
			_lock.Release();
		}
	}

	public async Task<IEnumerable<SearchResult>> QueryAsync(string query)
	{
		await EnsureInitializedAsync();

		await _lock.WaitAsync();
		try
		{
			var lowerQuery = query.ToLowerInvariant();
			var queryTerms = TokenizeQuery(lowerQuery);
			var results = new List<SearchResult>();

			foreach (var entry in _index.Values)
			{
				var score = CalculateScore(entry.Content, queryTerms);
				if (score > 0)
				{
					var header = new RecordHeader(entry.Id, entry.Created, entry.Metadata);
					results.Add(new SearchResult(entry.Id, score, header));
				}
			}

			return results.OrderByDescending(r => r.Score);
		}
		finally
		{
			_lock.Release();
		}
	}

	private static List<string> TokenizeQuery(string query)
	{
		return query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(t => t.Length > 2) // Ignore very short terms
			.Distinct()
			.ToList();
	}

	private static double CalculateScore(string content, List<string> queryTerms)
	{
		double score = 0;

		foreach (var term in queryTerms)
		{
			var count = CountOccurrences(content, term);
			if (count > 0)
			{
				// Score based on term frequency with diminishing returns
				score += Math.Log(count + 1) * 10;
			}
		}

		return score;
	}

	private static int CountOccurrences(string text, string pattern)
	{
		var count = 0;
		var index = 0;
		
		while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
		{
			count++;
			index += pattern.Length;
		}
		
		return count;
	}

	private async Task SaveIndexAsync()
	{
		if (!_isDirty) return;

		try
		{
			var entries = _index.Values.ToList();
			var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions 
			{ 
				WriteIndented = false 
			});
			
			var plaintext = System.Text.Encoding.UTF8.GetBytes(json);
			var encrypted = await _encryptionProvider.EncryptAsync(plaintext, "search-index");
			
			var tempPath = _indexFilePath + ".tmp";
			await File.WriteAllBytesAsync(tempPath, encrypted);
			File.Move(tempPath, _indexFilePath, true);
			
			_isDirty = false;
		}
		catch
		{
			// Fail silently to avoid disrupting the app
			// In production, this should be logged
		}
	}

	public async Task ClearAsync()
	{
		await _lock.WaitAsync();
		try
		{
			_index.Clear();
			_isDirty = true;
			await SaveIndexAsync();
			
			if (File.Exists(_indexFilePath))
			{
				File.Delete(_indexFilePath);
			}
		}
		finally
		{
			_lock.Release();
		}
	}
}
