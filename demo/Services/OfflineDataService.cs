using demo.Models;
using Cabinet.Abstractions;
using Cabinet.Core;
using Cabinet.Generated;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace demo.Services;

/// <summary>
/// Demonstrates Cabinet best practices:
/// - Using RecordSet for type-safe record management
/// - Aggregated file stores (single store for multiple record types)
/// - Both Stream-based and custom-encoded attachments
/// - Attachments as record properties
/// </summary>
public class OfflineDataService
{
	private readonly IOfflineStore _store;
	private readonly RecordSet<LessonRecord> _lessons;
	private readonly RecordSet<StudentRecord> _students;

	private static readonly string[] _subjects =
		["Maths", "Science", "English", "Art", "Geography", "Music"];

	private static readonly string[] _activities =
		["counted seagulls", "built a volcano", "painted a landscape",
		 "read a story", "played piano", "made a map"];

	private static readonly string[] _childNames = ["Alice", "Ben", "Chloe", "Dylan"];

	public OfflineDataService(IOfflineStore store)
	{
		_store = store;

		// Use source-generated RecordSet extensions for type-safe access
		_lessons = store.CreateLessonRecordRecordSet();
		_students = store.CreateStudentRecordRecordSet();
	}


	/// <summary>
	/// Generates sample records demonstrating both LessonRecord and StudentRecord.
	/// Shows FileAttachment usage both as separate parameter and as record properties.
	/// Uses RecordSet for type-safe record management.
	/// </summary>
	public async Task<(int count, TimeSpan duration)> GenerateAndSaveRecordsAsync(int count, bool includeAttachments = false)
	{
		var stopwatch = Stopwatch.StartNew();

		// Generate mix of lessons and students (60/40 split)
		var lessonsToGenerate = (int)(count * 0.6);
		var studentsToGenerate = count - lessonsToGenerate;

		// Generate lesson records
		for (var i = 0; i < lessonsToGenerate; i++)
		{
			var child = _childNames[Random.Shared.Next(_childNames.Length)];
			var lesson = new LessonRecord
			{
				Id			= Guid.NewGuid(),
				Date		= DateOnly.FromDateTime(DateTime.Today.AddDays(-Random.Shared.Next(30))),
				Subject		= _subjects[i % _subjects.Length],
				Description = $"{child} {_activities[Random.Shared.Next(_activities.Length)]} in {_subjects[i % _subjects.Length]} class.",
				Children	= [child],
				Tags		= [_subjects[i % _subjects.Length], child, "lesson"],
			};

			// Attachment bytes are stored as separate encrypted files, keyed on the record's own id.
			// The record itself carries only the returned metadata.
			if (includeAttachments)
			{
				await using var photoStream = await FileSystem.OpenAppPackageFileAsync("sample_image.png");
				var photoAttachment = new FileAttachment($"{child}_photo.png", "image/png", photoStream);

				var info = await _store.SaveAttachmentAsync(lesson.Id.ToString(), photoAttachment);
				lesson.Attachments = [info];
			}

			// RecordSet<T>.AddAsync - uses RecordSet for type-safe access
			await _lessons.AddAsync(lesson);
		}

		// Generate student records
		for (var i = 0; i < studentsToGenerate; i++)
		{
			var name = _childNames[Random.Shared.Next(_childNames.Length)];
			var student = new StudentRecord
			{
				Id				= $"student-{Guid.NewGuid()}",
				Name			= name,
				Age				= Random.Shared.Next(6, 13),
				Grade			= $"Grade {Random.Shared.Next(1, 7)}",
				Subjects		= [_subjects[Random.Shared.Next(_subjects.Length)]],
				EnrolmentDate	= DateTime.UtcNow.AddDays(-Random.Shared.Next(365)),
			};

			// Demonstrate the two ways of holding binary data:
			// 1. As an attachment - bytes stored separately, metadata on the record (preferred)
			// 2. As custom encoding inline in the record - you control the encoding, and pay the
			//    ~33% base64 overhead inside the record itself
			if (includeAttachments)
			{
				// Pattern 1: attachment
				await using var photoStream = await FileSystem.OpenAppPackageFileAsync("sample_image.png");
				var photoAttachment = new FileAttachment($"{name}_profile.png", "image/png", photoStream);

				student.ProfilePhoto = await _store.SaveAttachmentAsync(student.Id, photoAttachment);

				// Pattern 2: custom base64 encoding
				var certBytes = Encoding.UTF8.GetBytes($"CERTIFICATE:{name}:Age-{student.Age}");
				student.CertificateBase64 = Convert.ToBase64String(certBytes);
			}

			// RecordSet<T>.AddAsync - uses RecordSet for type-safe access
			await _students.AddAsync(student);
		}

		stopwatch.Stop();
		Debug.WriteLine($"Generated {lessonsToGenerate} lessons and {studentsToGenerate} students in {stopwatch.ElapsedMilliseconds}ms");
		return (count, stopwatch.Elapsed);
	}

	/// <summary>
	/// Search across both record types demonstrating unified search.
	/// </summary>
	public async Task<(int count, TimeSpan duration, IEnumerable<SearchResultWithData> results)> SearchRecordsAsync(string query)
	{
		var stopwatch = Stopwatch.StartNew();
		
		// Search across both record types using RecordSet
		var lessonResults = await _lessons.FindAsync(query);
		var studentResults = await _students.FindAsync(query);
		
		// Combine results
		var resultsWithData = new List<SearchResultWithData>();
		
		foreach (var lesson in lessonResults)
		{
			resultsWithData.Add(new SearchResultWithData(
				lesson.Id.ToString(),
				"Lesson", 
				$"{lesson.Subject} - {lesson.Date:yyyy-MM-dd}", 
				lesson.Description));
		}
		
		foreach (var student in studentResults)
		{
			resultsWithData.Add(new SearchResultWithData(
				student.Id,
				"Student", 
				student.Name, 
				$"Age {student.Age}, {student.Grade}"));
		}
		
		stopwatch.Stop();
		return (resultsWithData.Count, stopwatch.Elapsed, resultsWithData);
	}

	public Task<StudentRecord?> OpenStudentRecordAsync(string studentId) => _students.GetByIdAsync(studentId);

	public async Task<LessonRecord?> OpenLessonRecordAsync(Guid lessonId)
	{
		var lessonResults = await _lessons.FindAsync(lessonId.ToString());

		var lessonRecords = lessonResults as LessonRecord[] ?? [.. lessonResults];
		if (lessonRecords?.Count() != 1) return null;
		var lesson = lessonRecords.First();
		return lesson;
	}

	/// <summary>
	/// Reads an attachment's bytes back out of the store.
	/// </summary>
	/// <param name="recordId">The id of the record the attachment belongs to</param>
	/// <param name="attachment">The attachment metadata held on that record</param>
	/// <returns>The decrypted content, or null if the attachment is no longer present</returns>
	public async Task<byte[]?> ReadAttachmentAsync(string recordId, AttachmentInfo attachment)
	{
		await using var content = await _store.OpenAttachmentAsync(recordId, attachment.Name);
		if (content is null) return null;

		using var buffer = new MemoryStream();
		await content.CopyToAsync(buffer);
		return buffer.ToArray();
	}

	public Task<(int filesDeleted, TimeSpan duration)> PurgeDataAsync()
	{
		var stopwatch = Stopwatch.StartNew();
		
		// Get the offline data directory
		var cabinetPath = Path.Combine(FileSystem.AppDataDirectory, "Cabinet");
		
		int filesDeleted = 0;
		
		if (Directory.Exists(cabinetPath))
		{
			// Delete all files in subdirectories
			foreach (var subdir in new[] { "records", "attachments", "index" })
			{
				var subdirPath = Path.Combine(cabinetPath, subdir);
				if (Directory.Exists(subdirPath))
				{
					// Recursive: attachments are grouped into a directory per record.
					var files = Directory.GetFiles(subdirPath, "*", SearchOption.AllDirectories);
					foreach (var file in files)
					{
						File.Delete(file);
						filesDeleted++;
					}

					foreach (var directory in Directory.GetDirectories(subdirPath))
					{
						Directory.Delete(directory, recursive: true);
					}
				}
			}
		}
		
		stopwatch.Stop();
		return Task.FromResult((filesDeleted, stopwatch.Elapsed));
	}

	/// <summary>
	/// Get counts for each record type to show aggregated store usage.
	/// </summary>
	public (int lessonCount, int studentCount) GetRecordCounts()
	{
		// RecordSets track counts in memory
		var lessonCount = _lessons.Count();
		var studentCount = _students.Count();
		return (lessonCount, studentCount);
	}
}
