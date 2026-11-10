using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionInspector.Services;

namespace VisionInspector.ViewModels;

public partial class SettingsViewModel : ObservableObject, IActivatable
{
    [ObservableProperty] private double threshold;
    [ObservableProperty] private int intervalSeconds;
    [ObservableProperty] private int cameraIndex;
    [ObservableProperty] private bool cropEnabled = true;
    [ObservableProperty] private double cropPercent = 100;
    [ObservableProperty] private string pythonPath = "";
    [ObservableProperty] private string engineScriptPath = "";
    [ObservableProperty] private string message = "";
    [ObservableProperty] private string engineInfo = "";

    public string CapturesDir => AppServices.Settings.CapturesDir;
    public string ResultsDir => AppServices.Settings.ResultsDir;
    public string DbPath => AppServices.Settings.DbPath;

    public SettingsViewModel()
    {
        Load();
        AppServices.Engine.StateChanged += (_, msg) =>
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => EngineInfo = msg);
        EngineInfo = AppServices.Engine.StateMessage;
    }

    public void OnActivated()
    {
        Load();
        Message = "";
        EngineInfo = AppServices.Engine.StateMessage;
    }

    private void Load()
    {
        var s = AppServices.Settings;
        Threshold = s.Threshold;
        IntervalSeconds = s.IntervalSeconds;
        CameraIndex = s.CameraIndex;
        CropEnabled = s.CropEnabled;
        CropPercent = s.CropPercent;
        PythonPath = s.PythonPath;
        EngineScriptPath = s.EngineScriptPath;
    }

    private bool Apply(out bool engineChanged)
    {
        engineChanged = false;
        if (IntervalSeconds < 1 || IntervalSeconds > 600)
        {
            Message = "자동 검사 간격은 1~600초 사이로 입력하세요.";
            return false;
        }
        if (CameraIndex < 0 || CameraIndex > 9)
        {
            Message = "카메라 번호는 0~9 사이로 입력하세요.";
            return false;
        }
        var s = AppServices.Settings;
        engineChanged = s.PythonPath != PythonPath || s.EngineScriptPath != EngineScriptPath;
        s.Threshold = Math.Round(Threshold, 2);   // 기준값은 바로 적용된다 (엔진 재시작 불필요)
        s.IntervalSeconds = IntervalSeconds;
        s.CameraIndex = CameraIndex;
        s.CropEnabled = CropEnabled;
        s.CropPercent = Math.Round(CropPercent);
        AppServices.Camera.ConfigureCrop(s.CropEnabled, s.CropPercent);   // 웹캠이 켜져 있어도 바로 적용
        s.PythonPath = PythonPath.Trim();
        s.EngineScriptPath = EngineScriptPath.Trim();
        AppServices.SettingsService.Save(s);
        return true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!Apply(out var engineChanged)) return;
        Message = engineChanged
            ? "저장했습니다. Python/엔진 경로를 바꿨다면 [엔진 다시 시작]을 눌러야 적용됩니다."
            : "저장했습니다. 기준값은 바로 적용됩니다.";
    }

    [RelayCommand]
    private async Task RestartEngineAsync()
    {
        if (!Apply(out _)) return;
        Message = "엔진을 다시 시작합니다...";
        await AppServices.Engine.StartAsync(AppServices.Settings);
        Message = AppServices.Engine.State == EngineState.Ready ? "엔진이 다시 시작되었습니다." : "엔진 시작에 실패했습니다.";
    }
}
