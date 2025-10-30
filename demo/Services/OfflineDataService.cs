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
				Id = Guid.NewGuid(),
				Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-Random.Shared.Next(30))),
				Subject = _subjects[i % _subjects.Length],
				Description = $"{child} {_activities[Random.Shared.Next(_activities.Length)]} in {_subjects[i % _subjects.Length]} class.",
				Children = [child],
				Tags = [_subjects[i % _subjects.Length], child, "lesson"],
			};

			// Demonstrate attachment patterns for lessons:
			// 1. Add attachments to Attachments collection property (stored with record)
			if (includeAttachments)
			{
				var photoContent = Encoding.UTF8.GetBytes($"PHOTO data for {child}: {RandomNumberGenerator.GetInt32(1000000)}");
				var photoStream = new MemoryStream(photoContent);
				var photoAttachment = new FileAttachment($"{child}_photo.jpg", "image/jpeg", photoStream);

				lesson.Attachments = [photoAttachment];
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
				Id = $"student-{Guid.NewGuid()}",
				Name = name,
				Age = Random.Shared.Next(6, 13),
				Grade = $"Grade {Random.Shared.Next(1, 7)}",
				Subjects = [_subjects[Random.Shared.Next(_subjects.Length)]],
				EnrolmentDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(365)),
			};

			// Demonstrate attachment patterns for students:
			// 1. FileAttachment as property (ProfilePhoto)
			// 2. Custom encoding (CertificateBase64)
			if (includeAttachments)
			{
				// Pattern 1: FileAttachment property - Cabinet serializes it with the record
				var photoBytes = Encoding.UTF8.GetBytes($"PHOTO:{name}:{RandomNumberGenerator.GetInt32(1000000)}");
				student.ProfilePhoto = new FileAttachment($"{name}_profile.jpg", "image/jpeg", photoBytes);

				// Pattern 2: Custom base64 encoding - You control the encoding
				var certBytes = Encoding.UTF8.GetBytes($"CERTIFICATE:{name}:Age-{student.Age}");
				student.CertificateBase64 = Convert.ToBase64String(certBytes);
			}

			// RecordSet<T>.AddAsync - FileAttachment properties handled automatically
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
				"Lesson", 
				$"{lesson.Subject} - {lesson.Date:yyyy-MM-dd}", 
				lesson.Description));
		}
		
		foreach (var student in studentResults)
		{
			resultsWithData.Add(new SearchResultWithData(
				"Student", 
				student.Name, 
				$"Age {student.Age}, {student.Grade}"));
		}
		
		stopwatch.Stop();
		return (resultsWithData.Count, stopwatch.Elapsed, resultsWithData);
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

	/// <summary>
	/// Get counts for each record type to show aggregated store usage.
	/// </summary>
	public Task<(int lessonCount, int studentCount)> GetRecordCountsAsync()
	{
		// RecordSets track counts in memory
		var lessonCount = _lessons.Count();
		var studentCount = _students.Count();
		return Task.FromResult((lessonCount, studentCount));
	}

	public record SearchResultWithData(string RecordType, string Title, string Details);
}
