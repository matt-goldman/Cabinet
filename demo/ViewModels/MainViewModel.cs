using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using demo.Services;

namespace demo.ViewModels;

public partial class MainViewModel(OfflineDataService dataService) : ObservableObject
{
    [ObservableProperty]
    public partial string? Results { get; set; }

    [ObservableProperty]
    public partial int? RecordCount { get; set; }

    [ObservableProperty]
    public partial string? SearchTerm { get; set; }

    [RelayCommand]
    private Task GenerateRecords()
    {
        // TODO: Implement record generation logic here

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SearchRecords()
    {
        return Task.CompletedTask;
    }
}
