using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using demo.Models;
using demo.Pages;
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
	
	public ObservableCollection<SearchResultWithData> SearchResults { get; set; } = [];
	
	[ObservableProperty]
	public partial SearchResultWithData? SelectedRecord { get; set; }

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
		
		SearchResults.Clear();

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
				Results = $"🔍 Found {count} result(s) in {duration.TotalMilliseconds:F2}ms";
				foreach (var result in results)
				{
					SearchResults.Add(result);
				}
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

	[RelayCommand]
	public async Task ViewRecord()
	{
		if (SelectedRecord is null) return; 
		
		if (SelectedRecord.RecordType == "Student")
		{
			var student = await dataService.OpenStudentRecordAsync(SelectedRecord.Id);
			await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(new ResultPage(dataService, null, student));
		}
		else if (SelectedRecord.RecordType == "Lesson")// && Guid.TryParse(SelectedRecord.Id, out var id))
		{
			var lesson = await dataService.OpenLessonRecordAsync(SelectedRecord.Id);
			await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(new ResultPage(dataService, lesson, null));
		}
	}

	private void UpdateRecordCounts()
	{
		var (lessonCount, studentCount) = dataService.GetRecordCounts();
		LessonCount = lessonCount;
		StudentCount = studentCount;
	}
}
