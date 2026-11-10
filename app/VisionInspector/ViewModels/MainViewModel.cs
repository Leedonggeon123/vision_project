using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionInspector.Converters;
using VisionInspector.Services;

namespace VisionInspector.ViewModels;

/// <summary>화면이 열릴 때마다 데이터를 새로 읽어야 하는 뷰모델</summary>
public interface IActivatable
{
    void OnActivated();
}

public record NavItem(string Title, string Icon, ObservableObject ViewModel);

public partial class MainViewModel : ObservableObject
{
    public LiveInspectionViewModel Live { get; }
    public HistoryViewModel History { get; }
    public DashboardViewModel Dashboard { get; }
    public SettingsViewModel Settings { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty] private NavItem? selectedNav;
    [ObservableProperty] private ObservableObject? currentViewModel;
    [ObservableProperty] private string engineStatusText = "";
    [ObservableProperty] private Brush engineStatusBrush = Theme.IdleBrush;

    public MainViewModel()
    {
        Live = new LiveInspectionViewModel();
        History = new HistoryViewModel();
        Dashboard = new DashboardViewModel();
        Settings = new SettingsViewModel();

        NavItems = new ObservableCollection<NavItem>
        {
            new("실시간 검사", "", Live),
            new("이력 조회", "", History),
            new("통계 대시보드", "", Dashboard),
            new("설정", "", Settings),
        };
        SelectedNav = NavItems[0];

        AppServices.Engine.StateChanged += (state, message) =>
            Application.Current.Dispatcher.BeginInvoke(() => UpdateEngineStatus(state, message));
        UpdateEngineStatus(AppServices.Engine.State, AppServices.Engine.StateMessage);

        // 프로그램이 켜지면 엔진도 같이 켠다 (모델을 한 번만 불러오도록 계속 켜 둔다)
        _ = AppServices.Engine.StartAsync(AppServices.Settings);
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        CurrentViewModel = value?.ViewModel;
        (value?.ViewModel as IActivatable)?.OnActivated();
    }

    private void UpdateEngineStatus(EngineState state, string message)
    {
        EngineStatusText = message;
        EngineStatusBrush = state switch
        {
            EngineState.Ready => Theme.OkBrush,
            EngineState.Starting => Theme.WarnBrush,
            EngineState.Error => Theme.NgBrush,
            _ => Theme.IdleBrush,
        };
    }
}
