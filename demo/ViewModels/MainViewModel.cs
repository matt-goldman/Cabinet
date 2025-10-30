using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using demo.Services;

namespace demo.ViewModels;

public partial class MainViewModel(OfflineDataService dataService) : ObservableObject
{
	[ObservableProperty]
	public partial string? Results { get; set; }

	[ObservableProperty]
	public partial int RecordCount { get; set; } = 10;

	[ObservableProperty]
	public partial string? SearchTerm { get; set; }

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial bool IncludeAttachments { get; set; }

	[ObservableProperty]
	public partial int LessonCount { get; set; }

	[ObservableProperty]
	public partial int StudentCount { get; set; }

	[RelayCommand]
	private async Task GenerateRecords()
	{
		if (IsBusy) return;

		try
		{
			IsBusy = true;
			Results = "Generating records...";

			var (count, duration) = await dataService.GenerateAndSaveRecordsAsync(RecordCount, IncludeAttachments);

			// Update counts to show aggregated store usage
			UpdateRecordCounts();

			Results = $"✅ Generated and saved {count} record(s) in {duration.TotalMilliseconds:F2}ms\n" +
				$"📊 Total: {LessonCount} lessons, {StudentCount} students";
			
			if (IncludeAttachments)
			{
				Results += $"\n📎 Attachments included:\n" +
					"  • Lessons: PDF (SaveAsync param) + Photo (Attachments property)\n" +
					"  • Students: ProfilePhoto (FileAttachment property) + CertificateBase64 (custom encoding)";
			}
		}
		catch (Exception ex)
		{
			Results = $"❌ Error: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task SearchRecords()
	{
		if (IsBusy) return;

		if (string.IsNullOrWhiteSpace(SearchTerm))
		{
			Results = "⚠️ Please enter a search term";
			return;
		}

		try
		{
			IsBusy = true;
			Results = $"Searching for '{SearchTerm}'...";

			var (count, duration, results) = await dataService.SearchRecordsAsync(SearchTerm);

			if (count == 0)
			{
				Results = $"🔍 No results found for '{SearchTerm}' (searched in {duration.TotalMilliseconds:F2}ms)";
			}
			else
			{
				var resultBuilder = new StringBuilder();
				resultBuilder.Append($"🔍 Found {count} result(s) in {duration.TotalMilliseconds:F2}ms\n");
				resultBuilder.Append($"Searched across both LessonRecord and StudentRecord types\n\n");
				
				foreach (var result in results.Take(10))
				{
					resultBuilder.Append($"📝 [{result.RecordType}] {result.Title}\n");
					resultBuilder.Append($"   {result.Details}\n\n");
				}

				if (count > 10)
				{
					resultBuilder.Append($"... and {count - 10} more");
				}

				Results = resultBuilder.ToString();
			}
		}
		catch (Exception ex)
		{
			Results = $"❌ Search error: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task PurgeData()
	{
		if (IsBusy) return;

		try
		{
			IsBusy = true;
			Results = "🗑️ Purging all data...";

			var (filesDeleted, duration) = await dataService.PurgeDataAsync();

			// Reset counts
			LessonCount = 0;
			StudentCount = 0;

			Results = $"✅ Purged {filesDeleted} file(s) in {duration.TotalMilliseconds:F2}ms\n" +
					  $"All records, attachments, and index data have been deleted.";
		}
		catch (Exception ex)
		{
			Results = $"❌ Purge error: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
		}
	}

	private void UpdateRecordCounts()
	{
		var (lessonCount, studentCount) = dataService.GetRecordCounts();
		LessonCount = lessonCount;
		StudentCount = studentCount;
	}
}
