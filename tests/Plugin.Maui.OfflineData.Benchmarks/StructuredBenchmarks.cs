using System.Diagnostics;
using System.Runtime.InteropServices;
using Plugin.Maui.OfflineData.Core;
using Plugin.Maui.OfflineData.Index;
using Plugin.Maui.OfflineData.Security;

namespace Plugin.Maui.OfflineData.Benchmarks;

/// <summary>
/// Structured benchmark runner for aggregate record files.
/// Tests performance when grouping multiple records into logical files,
/// as recommended for real-world use cases.
/// </summary>
public static class StructuredBenchmarks
{
	public static async Task Run()
	{
		Console.WriteLine("Plugin.Maui.OfflineData Structured Benchmarks");
		Console.WriteLine("==============================================");
		Console.WriteLine("Testing aggregate record files (multiple records per file)");
		Console.WriteLine();

		var results = new List<BenchmarkResult>();

		// Test with different dataset sizes
		var sizes = new[] { 10, 100, 1000, 5000 };

		foreach (var size in sizes)
		{
			Console.WriteLine($"Running structured benchmarks with {size} total records...");
			var result = await RunBenchmark(size);
			results.Add(result);
			Console.WriteLine($"  Completed in {result.TotalTime:F2}s");
			Console.WriteLine();
		}

		// Generate markdown report
		Console.WriteLine();
		Console.WriteLine("## Structured Benchmark Results");
		Console.WriteLine();
		Console.WriteLine("| Dataset Size | Files | Save & Index | Search (single) | Search (multi) | Load File | Cold Start | Memory |");
		Console.WriteLine("|--------------|-------|--------------|-----------------|----------------|-----------|------------|---------|");

		foreach (var result in results)
		{
			Console.WriteLine($"| {result.RecordCount,12} | {result.FileCount,5} | {result.SaveTime,9:F2} ms | {result.SearchSingleTime,12:F2} ms | {result.SearchMultiTime,11:F2} ms | {result.LoadTime,6:F2} ms | {result.ColdStartTime,7:F2} ms | {result.MemoryMB,6:F2} MB |");
		}

		Console.WriteLine();
		Console.WriteLine("### Key Observations");
		Console.WriteLine();
		Console.WriteLine($"- **Aggregate file structure**: ~{results.Last().FileCount} files for {results.Last().RecordCount:N0} records");
		Console.WriteLine($"- **Fast search**: {results.Last().SearchSingleTime:F2} ms search across {results.Last().RecordCount:N0} records");
		Console.WriteLine($"- **Efficient indexing**: Average time per file: ~{results.Last().SaveTime / results.Last().FileCount:F2} ms");
		Console.WriteLine($"- **Cold start**: {results.Last().ColdStartTime:F2} ms to reload index with {results.Last().RecordCount:N0} records");
		Console.WriteLine($"- **Memory usage**: ~{results.Last().MemoryMB:F2} MB for {results.Last().RecordCount:N0} indexed records");
		Console.WriteLine();
		Console.WriteLine($"_Benchmarks run on .NET {Environment.Version} ({RuntimeInformation.FrameworkDescription})_");
		Console.WriteLine($"_Hardware: {Environment.ProcessorCount} cores, {Environment.OSVersion}_");
	}

	private static async Task<BenchmarkResult> RunBenchmark(int totalRecords)
	{
		var testPath = Path.Combine(Path.GetTempPath(), $"StructuredBenchmarks_{Guid.NewGuid()}");
		Directory.CreateDirectory(testPath);

		// Use current year for lesson file naming to ensure consistency
		const int BaseYear = 2024;
		var subjectNames = new[] { "Science", "Math", "History", "English", "Art" };

		try
		{
			var key = new byte[32];
			Random.Shared.NextBytes(key);
			var encryption = new AesGcmEncryptionProvider(key);
			var indexProvider = new PersistentIndexProvider(testPath, encryption);
			var store = new FileOfflineStore(testPath, encryption, indexProvider);

			var sw = Stopwatch.StartNew();

			// Calculate structured distribution:
			// - Children: all in one file
			// - Subjects: all in one file
			// - Lessons: grouped by year (assume ~250 records per year for larger datasets)
			var childrenCount = Math.Max(1, totalRecords / 10); // 10% children
			var subjectsCount = Math.Max(1, totalRecords / 20); // 5% subjects
			var lessonsCount = totalRecords - childrenCount - subjectsCount; // remaining are lessons
			var lessonsPerYear = Math.Max(1, Math.Min(250, lessonsCount / 2)); // 2-n years of lessons
			var yearCount = Math.Max(1, (int)Math.Ceiling((double)lessonsCount / lessonsPerYear));

			int fileCount = 2 + yearCount; // children + subjects + year files

			// Benchmark 1: Save and Index with structured files
			var saveStartMem = GC.GetTotalMemory(true);
			var saveStart = sw.ElapsedMilliseconds;

			// Save all children in one file
			var children = new List<Child>();
			for (int i = 0; i < childrenCount; i++)
			{
				children.Add(new Child
				{
					Id = $"child-{i}",
					Name = $"Child {i}",
					BirthDate = DateOnly.FromDateTime(DateTime.Today.AddYears(-10 - i % 5))
				});
			}
			await store.SaveAsync("children", new ChildrenCollection { Children = children });

			// Save all subjects in one file
			var subjects = new List<Subject>();
			for (int i = 0; i < subjectsCount; i++)
			{
				subjects.Add(new Subject
				{
					Id = $"subject-{i}",
					Name = subjectNames[i % subjectNames.Length],
					Description = $"Subject about {i % subjectNames.Length} with content about learning and education"
				});
			}
			await store.SaveAsync("subjects", new SubjectsCollection { Subjects = subjects });

			// Save lessons grouped by year
			int lessonIndex = 0;
			for (int year = 0; year < yearCount; year++)
			{
				var lessons = new List<Lesson>();
				var lessonsInYear = Math.Min(lessonsPerYear, lessonsCount - lessonIndex);
				
				for (int i = 0; i < lessonsInYear; i++)
				{
					lessons.Add(new Lesson
					{
						Id = $"lesson-{lessonIndex}",
						Date = DateOnly.FromDateTime(DateTime.Today.AddYears(-year).AddDays(-i)),
						Subject = subjects[lessonIndex % subjectsCount].Name,
						Description = $"Lesson {lessonIndex} about seagulls and beaches with searchable educational content",
						Children = new List<string> { children[lessonIndex % childrenCount].Name }
					});
					lessonIndex++;
				}
				
				await store.SaveAsync($"lessons-{BaseYear - year}", new LessonsCollection { Lessons = lessons });
			}

			var saveTime = sw.ElapsedMilliseconds - saveStart;
			var saveEndMem = GC.GetTotalMemory(false);

			// Benchmark 2: Search (single term)
			var searchStart = sw.ElapsedMilliseconds;
			for (int i = 0; i < 10; i++)
			{
				var results = await store.SearchAsync("seagulls");
				_ = results.ToList();
			}
			var searchSingleTime = (sw.ElapsedMilliseconds - searchStart) / 10.0;

			// Benchmark 3: Search (multiple terms)
			searchStart = sw.ElapsedMilliseconds;
			for (int i = 0; i < 10; i++)
			{
				var results = await store.SearchAsync("seagulls beaches educational");
				_ = results.ToList();
			}
			var searchMultiTime = (sw.ElapsedMilliseconds - searchStart) / 10.0;

			// Benchmark 4: Load single file (lessons from a year)
			var loadStart = sw.ElapsedMilliseconds;
			for (int i = 0; i < 10; i++)
			{
				var lessonsFile = await store.LoadAsync<LessonsCollection>($"lessons-{BaseYear}");
			}
			var loadTime = (sw.ElapsedMilliseconds - loadStart) / 10.0;

			// Benchmark 5: Cold start (reload index)
			var coldStartIndexProvider = new PersistentIndexProvider(testPath, encryption);
			var coldStart = sw.ElapsedMilliseconds;
			var coldResults = await coldStartIndexProvider.QueryAsync("seagulls");
			_ = coldResults.ToList();
			var coldStartTime = sw.ElapsedMilliseconds - coldStart;

			var totalTime = sw.Elapsed.TotalSeconds;
			var memoryMB = (saveEndMem - saveStartMem) / (1024.0 * 1024.0);

			return new BenchmarkResult
			{
				RecordCount = totalRecords,
				FileCount = fileCount,
				SaveTime = saveTime,
				SearchSingleTime = searchSingleTime,
				SearchMultiTime = searchMultiTime,
				LoadTime = loadTime,
				ColdStartTime = coldStartTime,
				TotalTime = totalTime,
				MemoryMB = Math.Max(0, memoryMB)
			};
		}
		finally
		{
			if (Directory.Exists(testPath))
			{
				Directory.Delete(testPath, recursive: true);
			}
		}
	}

	private class Child
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public DateOnly BirthDate { get; set; }
	}

	private class ChildrenCollection
	{
		public List<Child> Children { get; set; } = [];
	}

	private class Subject
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
	}

	private class SubjectsCollection
	{
		public List<Subject> Subjects { get; set; } = [];
	}

	private class Lesson
	{
		public string Id { get; set; } = string.Empty;
		public DateOnly Date { get; set; }
		public string Subject { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public List<string> Children { get; set; } = [];
	}

	private class LessonsCollection
	{
		public List<Lesson> Lessons { get; set; } = [];
	}

	private class BenchmarkResult
	{
		public int RecordCount { get; set; }
		public int FileCount { get; set; }
		public double SaveTime { get; set; }
		public double SearchSingleTime { get; set; }
		public double SearchMultiTime { get; set; }
		public double LoadTime { get; set; }
		public double ColdStartTime { get; set; }
		public double TotalTime { get; set; }
		public double MemoryMB { get; set; }
	}
}
