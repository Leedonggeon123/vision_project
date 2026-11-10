using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionInspector.Models;
using VisionInspector.Services;

namespace VisionInspector.ViewModels;

public partial class HistoryViewModel : ObservableObject, IActivatable
{
    public ObservableCollection<InspectionResult> Items { get; } = new();
    public string[] ResultOptions { get; } = ["전체", "OK", "NG"];

    [ObservableProperty] private string resultFilter = "전체";
    [ObservableProperty] private DateTime? fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime? toDate = DateTime.Today;
    [ObservableProperty] private InspectionResult? selected;
    [ObservableProperty] private string summary = "";

    public void OnActivated() => Load();

    [RelayCommand]
    private void Load()
    {
        var list = AppServices.Db.Query(FromDate, ToDate, ResultFilter);
        Items.Clear();
        foreach (var r in list) Items.Add(r);
        var ng = list.Count(r => r.IsNg);
        Summary = $"총 {list.Count}건 · OK {list.Count - ng}건 · NG {ng}건";
        Selected = Items.FirstOrDefault();
    }
}
