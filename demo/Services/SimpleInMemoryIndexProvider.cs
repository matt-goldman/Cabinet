using System.Collections.Concurrent;
using Cabinet.Abstractions;
using Cabinet.Core;

namespace demo.Services;

/// <summary>
/// A simple in-memory index provider for demo purposes.
/// In production, this should be replaced with EasyIndex or another persistent index provider.
/// 
/// Limitations:
/// - Index is lost on app restart (not persistent)
/// - Does not store original record metadata (creation date, etc.)
/// - Simple substring matching without tokenisation
/// - No support for advanced query operators
/// </summary>
public class SimpleInMemoryIndexProvider : IIndexProvider
{
	private readonly ConcurrentDictionary<string, string> _index = new();

	public Task IndexAsync(string id, string content, IDictionary<string, string> metadata)
	{
		// Note: Metadata is intentionally not stored in this simple demo implementation.
		// A production implementation (like EasyIndex) should persist metadata for:
		// - Filtering by date ranges
		// - Tag-based queries
		// - Record type filtering
		// - Enhanced result ranking
		
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
				// Simple scoring based on the number of times the query appears
				var occurrences = CountOccurrences(content, lowerQuery);
				var score = occurrences * 10.0; // Scale up for better visibility
				
				// Note: Using current timestamp as we don't persist original record metadata.
				// A production implementation should store and return actual creation timestamps.
				var header = new RecordHeader(id, DateTimeOffset.UtcNow);
				results.Add(new SearchResult(id, score, header));
			}
		}

		// Sort by score descending
		return Task.FromResult<IEnumerable<SearchResult>>(results.OrderByDescending(r => r.Score));
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

	public Task ClearAsync()
	{
		_index.Clear();
		return Task.CompletedTask;
	}
}
