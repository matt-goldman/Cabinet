using System.Diagnostics;
using System.Security.Cryptography;
using demo.Models;
using Plugin.Maui.OfflineData.Abstractions;
using Plugin.Maui.OfflineData.Core;
using Plugin.Maui.OfflineData.Security;

namespace demo.Services;

public class OfflineDataService
{
	private readonly IOfflineStore _store;
	private static readonly string[] Subjects =
		["Maths", "Science", "English", "Art", "Geography", "Music"];

	private static readonly string[] Activities =
		["counted seagulls", "built a volcano", "painted a landscape",
		 "read a story", "played piano", "made a map"];

	private static readonly string[] Children = ["Alice", "Ben", "Chloe", "Dylan"];

	public OfflineDataService(IOfflineStore store)
	{
		_store = store;
	}

	public async Task<(int count, TimeSpan duration)> GenerateAndSaveRecordsAsync(int count, bool includeAttachments = false)
	{
		var stopwatch = Stopwatch.StartNew();

		for (int i = 0; i < count; i++)
		{
			var record = GenerateRandomRecord();
			var attachments = includeAttachments ? new[] { await CreateRandomAttachmentAsync(record.Id.ToString()) } : null;
			await _store.SaveAsync(record.Id.ToString(), record, attachments);
		}

		stopwatch.Stop();
		return (count, stopwatch.Elapsed);
	}

	public async Task<(int count, TimeSpan duration, IEnumerable<SearchResult> results)> SearchRecordsAsync(string query)
	{
		var stopwatch = Stopwatch.StartNew();
		var results = await _store.SearchAsync(query);
		stopwatch.Stop();

		return (results.Count(), stopwatch.Elapsed, results);
	}

	private LessonRecord GenerateRandomRecord()
	{
		var subject = Subjects[Random.Shared.Next(Subjects.Length)];
		var activity = Activities[Random.Shared.Next(Activities.Length)];
		var child = Children[Random.Shared.Next(Children.Length)];
		var date = DateOnly.FromDateTime(DateTime.Today.AddDays(-Random.Shared.Next(30)));

		return new LessonRecord
		{
			Date = date,
			Subject = subject,
			Description = $"{child} {activity} in {subject} class.",
			Children = [child],
			Tags = [subject.ToLower(), "learning"]
		};
	}

	private async Task<FileAttachment> CreateRandomAttachmentAsync(string recordId)
	{
		var bytes = RandomNumberGenerator.GetBytes(512);
		var stream = new MemoryStream(bytes);
		return await Task.FromResult(new FileAttachment($"random-{recordId}.bin", "application/octet-stream", stream));
	}
}
