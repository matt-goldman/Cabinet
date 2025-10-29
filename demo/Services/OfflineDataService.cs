using demo.Models;
using Cabinet.Abstractions;
using Cabinet.Core;
using System.Diagnostics;
using System.Security.Cryptography;

namespace demo.Services;

public class OfflineDataService(IOfflineStore store)
{
    private static readonly string[] _subjects =
		["Maths", "Science", "English", "Art", "Geography", "Music"];

    private static readonly string[] _activities =
		["counted seagulls", "built a volcano", "painted a landscape",
		 "read a story", "played piano", "made a map"];

	private static readonly string[] _children = ["Alice", "Ben", "Chloe", "Dylan"];


	public async Task<(int count, TimeSpan duration)> GenerateAndSaveRecordsAsync(int count, bool includeAttachments = false)
	{
		var stopwatch = Stopwatch.StartNew();

		for (int i = 0; i < count; i++)
		{
			var record = GenerateRandomRecord();
			var attachments = includeAttachments ? new[] { await CreateRandomAttachmentAsync(record.Id.ToString()) } : null;
			await store.SaveAsync(record.Id.ToString(), record, attachments);
		}

		stopwatch.Stop();
		return (count, stopwatch.Elapsed);
	}

	public async Task<(int count, TimeSpan duration, IEnumerable<SearchResultWithData> results)> SearchRecordsAsync(string query)
	{
		var stopwatch = Stopwatch.StartNew();
		var searchResults = await store.FindAsync(query);
		
		// Load the actual records for each search result to get metadata
		var resultsWithData = new List<SearchResultWithData>();
		foreach (var result in searchResults)
		{
			var record = await store.LoadAsync<LessonRecord>(result.RecordId);
			if (record != null)
			{
				resultsWithData.Add(new SearchResultWithData(result, record));
			}
		}
		
		stopwatch.Stop();

		return (resultsWithData.Count, stopwatch.Elapsed, resultsWithData);
	}

	public Task<(int filesDeleted, TimeSpan duration)> PurgeDataAsync()
	{
		var stopwatch = Stopwatch.StartNew();
		
		// Get the offline data directory
		var CabinetPath = Path.Combine(FileSystem.AppDataDirectory, "Cabinet");
		
		int filesDeleted = 0;
		
		if (Directory.Exists(CabinetPath))
		{
			// Delete all files in subdirectories
			foreach (var subdir in new[] { "records", "attachments", "index" })
			{
				var subdirPath = Path.Combine(CabinetPath, subdir);
				if (Directory.Exists(subdirPath))
				{
					var files = Directory.GetFiles(subdirPath);
					foreach (var file in files)
					{
						File.Delete(file);
						filesDeleted++;
					}
				}
			}
		}
		
		stopwatch.Stop();
		return Task.FromResult((filesDeleted, stopwatch.Elapsed));
	}

	public record SearchResultWithData(SearchResult SearchResult, LessonRecord Record);

	private static LessonRecord GenerateRandomRecord()
	{
		var subject = _subjects[Random.Shared.Next(_subjects.Length)];
		var activity = _activities[Random.Shared.Next(_activities.Length)];
		var child = _children[Random.Shared.Next(_children.Length)];
		var date = DateOnly.FromDateTime(DateTime.Today.AddDays(-Random.Shared.Next(30)));

		return new LessonRecord
		{
			Date		= date,
			Subject		= subject,
			Description = $"{child} {activity} in {subject} class.",
			Children	= [child],
			Tags		= [subject.ToLower(), "learning"]
		};
	}

    private static async Task<FileAttachment> CreateRandomAttachmentAsync(string recordId)
	{
		var bytes = RandomNumberGenerator.GetBytes(512);
		var stream = new MemoryStream(bytes);
		return await Task.FromResult(new FileAttachment($"random-{recordId}.bin", "application/octet-stream", stream));
	}
}
