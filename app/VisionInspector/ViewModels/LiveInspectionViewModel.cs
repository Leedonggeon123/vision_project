using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VisionInspector.Converters;
using VisionInspector.Models;
using VisionInspector.Services;

namespace VisionInspector.ViewModels;

public partial class LiveInspectionViewModel : ObservableObject, IActivatable
{
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    private readonly DispatcherTimer _autoTimer = new();
    private CancellationTokenSource? _folderCts;

    public ObservableCollection<InspectionResult> Recent { get; } = new();

    [ObservableProperty] private ImageSource? previewImage;
    [ObservableProperty] private string? heatmapPath;
    [ObservableProperty] private string resultText = "대기";
    [ObservableProperty] private Brush resultBrush = Theme.IdleBrush;
    [ObservableProperty] private string scoreText = "-";
    [ObservableProperty] private string thresholdText = "-";
    [ObservableProperty] private string elapsedText = "-";
    [ObservableProperty] private string statusMessage = "엔진이 준비되면 검사할 수 있습니다.";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isCameraOn;
    [ObservableProperty] private bool isAutoRunning;
    [ObservableProperty] private bool isFolderRunning;
    [ObservableProperty] private bool isEngineReady;
    [ObservableProperty] private int todayOk;
    [ObservableProperty] private int todayNg;
    [ObservableProperty] private string cameraButtonText = "웹캠 켜기";
    [ObservableProperty] private string autoButtonText = "자동 검사 시작";
    [ObservableProperty] private string folderButtonText = "폴더 연속 검사";

    public LiveInspectionViewModel()
    {
        IsEngineReady = AppServices.Engine.State == EngineState.Ready;
        AppServices.Engine.StateChanged += (state, _) =>
            Application.Current.Dispatcher.BeginInvoke(() => IsEngineReady = state == EngineState.Ready);

        AppServices.Camera.FrameReady += frame =>
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (IsCameraOn) PreviewImage = frame;
            });

        AppServices.Inspection.ResultAdded += OnResultAdded;

        _autoTimer.Tick += async (_, _) =>
        {
            if (!IsBusy) await InspectCameraFrameAsync();   // 이전 검사가 안 끝났으면 이번 차례는 건너뜀
        };

        LoadToday();
    }

    public void OnActivated() => LoadToday();

    private void LoadToday()
    {
        var s = AppServices.Db.Summary(DateTime.Today);
        TodayOk = s.Ok;
        TodayNg = s.Ng;
    }

    private void OnResultAdded(InspectionResult r)
    {
        Recent.Insert(0, r);
        while (Recent.Count > 12) Recent.RemoveAt(Recent.Count - 1);
        if (r.IsNg) TodayNg++; else TodayOk++;
    }

    // ---------- 웹캠 ----------
    [RelayCommand]
    private void ToggleCamera()
    {
        if (IsCameraOn)
        {
            StopAuto();
            AppServices.Camera.Stop();
            IsCameraOn = false;
            CameraButtonText = "웹캠 켜기";
            StatusMessage = "웹캠을 껐습니다.";
            return;
        }
        var s = AppServices.Settings;
        if (AppServices.Camera.Start(s.CameraIndex, s.CameraWidth, s.CameraHeight))
        {
            IsCameraOn = true;
            CameraButtonText = "웹캠 끄기";
            StatusMessage = $"웹캠 미리보기 중입니다. (해상도 {AppServices.Camera.FrameWidth}x{AppServices.Camera.FrameHeight})";
        }
        else
        {
            StatusMessage = $"웹캠을 열 수 없습니다. (카메라 번호 {AppServices.Settings.CameraIndex}) 설정에서 번호를 확인하거나 파일/폴더 검사를 사용하세요.";
        }
    }

    [RelayCommand]
    private async Task InspectNowAsync()
    {
        if (!IsCameraOn)
        {
            StatusMessage = "웹캠을 먼저 켜거나, [파일 선택]으로 검사하세요.";
            return;
        }
        await InspectCameraFrameAsync();
    }

    private async Task InspectCameraFrameAsync()
    {
        var file = Path.Combine(AppServices.Settings.CapturesDir, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        if (!AppServices.Camera.SaveFrame(file))
        {
            StatusMessage = "웹캠 화면이 아직 들어오지 않았습니다.";
            return;
        }
        await RunInspectionAsync(file, "camera");
    }

    // ---------- 자동 간격 ----------
    [RelayCommand]
    private void ToggleAuto()
    {
        if (IsAutoRunning)
        {
            StopAuto();
            StatusMessage = "자동 검사를 멈췄습니다.";
            return;
        }
        if (!IsCameraOn)
        {
            StatusMessage = "자동 검사는 웹캠이 켜져 있어야 합니다. (웹캠이 없으면 [폴더 연속 검사]를 사용하세요)";
            return;
        }
        _autoTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, AppServices.Settings.IntervalSeconds));
        _autoTimer.Start();
        IsAutoRunning = true;
        AutoButtonText = "자동 검사 중지";
        StatusMessage = $"자동 검사 중입니다. ({AppServices.Settings.IntervalSeconds}초마다)";
    }

    private void StopAuto()
    {
        _autoTimer.Stop();
        IsAutoRunning = false;
        AutoButtonText = "자동 검사 시작";
    }

    // ---------- 파일 / 폴더 ----------
    [RelayCommand]
    private async Task PickFileAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "검사할 사진 선택",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp|모든 파일|*.*",
            InitialDirectory = Directory.Exists(Path.Combine(ProjectPaths.Root, "data"))
                ? Path.Combine(ProjectPaths.Root, "data") : null,
        };
        if (dlg.ShowDialog() != true) return;
        PreviewImage = PathToImageConverter.Load(dlg.FileName);
        await RunInspectionAsync(dlg.FileName, "file");
    }

    [RelayCommand]
    private async Task ToggleFolderRunAsync()
    {
        if (IsFolderRunning)
        {
            _folderCts?.Cancel();
            return;
        }
        var dlg = new OpenFolderDialog { Title = "검사할 사진이 들어 있는 폴더 선택" };
        if (dlg.ShowDialog() != true) return;

        var files = Directory.EnumerateFiles(dlg.FolderName)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
        {
            StatusMessage = "그 폴더에는 사진이 없습니다.";
            return;
        }

        _folderCts = new CancellationTokenSource();
        var token = _folderCts.Token;
        IsFolderRunning = true;
        FolderButtonText = "폴더 검사 중지";
        try
        {
            var i = 0;
            foreach (var f in files)
            {
                token.ThrowIfCancellationRequested();
                i++;
                StatusMessage = $"폴더 연속 검사 {i}/{files.Count}: {Path.GetFileName(f)}";
                PreviewImage = PathToImageConverter.Load(f);
                await RunInspectionAsync(f, "file");
                if (i < files.Count)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, AppServices.Settings.IntervalSeconds)), token);
            }
            StatusMessage = $"폴더 연속 검사를 마쳤습니다. ({files.Count}장)";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "폴더 연속 검사를 멈췄습니다.";
        }
        finally
        {
            IsFolderRunning = false;
            FolderButtonText = "폴더 연속 검사";
        }
    }

    // ---------- 공통 ----------
    private async Task RunInspectionAsync(string imagePath, string source)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var r = await AppServices.Inspection.InspectAsync(imagePath, source);
            ResultText = r.Result;
            ResultBrush = r.IsNg ? Theme.NgBrush : Theme.OkBrush;
            ScoreText = r.Score.ToString("F3");
            ThresholdText = r.Threshold.ToString("F2");
            ElapsedText = $"{r.ElapsedMs} ms";
            HeatmapPath = r.HeatmapPath;
            if (!IsFolderRunning)   // 폴더 연속 검사 중에는 진행 상황 문구를 그대로 둔다
                StatusMessage = $"검사 완료: {r.Result} (점수 {r.Score:F3}, {r.ElapsedMs} ms)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"검사 실패: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
