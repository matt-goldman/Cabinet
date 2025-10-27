using demo.Models;
using Plugin.Maui.OfflineData.Abstractions;
using Plugin.Maui.OfflineData.Core;
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

	public async Task<(int count, TimeSpan duration, IEnumerable<SearchResult> results)> SearchRecordsAsync(string query)
	{
		var stopwatch = Stopwatch.StartNew();
		var results = await store.SearchAsync(query);
		stopwatch.Stop();

		return (results.Count(), stopwatch.Elapsed, results);
	}

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
