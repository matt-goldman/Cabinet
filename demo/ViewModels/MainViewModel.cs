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

	[RelayCommand]
	private async Task GenerateRecords()
	{
		if (IsBusy) return;

		try
		{
			IsBusy = true;
			Results = "Generating records...";

			var (count, duration) = await dataService.GenerateAndSaveRecordsAsync(RecordCount, IncludeAttachments);

			Results = $"✅ Generated and saved {count} record(s) in {duration.TotalMilliseconds:F2}ms";
			if (IncludeAttachments)
			{
				Results += $"\n📎 Each record includes a random attachment";
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
				var resultText = $"🔍 Found {count} result(s) for '{SearchTerm}' in {duration.TotalMilliseconds:F2}ms\n\n";
				resultText += "Record IDs:\n";
				foreach (var result in results.Take(10))
				{
					resultText += $"  • {result.RecordId} (Score: {result.Score:F2})\n";
				}

				if (count > 10)
				{
					resultText += $"  ... and {count - 10} more";
				}

				Results = resultText;
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
}
