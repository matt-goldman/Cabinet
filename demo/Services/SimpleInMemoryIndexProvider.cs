using System.Collections.Concurrent;
using Plugin.Maui.OfflineData.Abstractions;
using Plugin.Maui.OfflineData.Core;

namespace demo.Services;

/// <summary>
/// A simple in-memory index provider for demo purposes.
/// In production, this should be replaced with EasyIndex or another persistent index provider.
/// </summary>
public class SimpleInMemoryIndexProvider : IIndexProvider
{
	private readonly ConcurrentDictionary<string, string> _index = new();

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
				// Simple scoring based on the number of times the query appears
				var occurrences = CountOccurrences(content, lowerQuery);
				var score = occurrences * 10.0; // Scale up for better visibility
				
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
}
