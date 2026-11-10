using System.Text.Json.Serialization;

namespace VisionInspector.Models;

/// <summary>검사 한 건의 기록 (SQLite 한 줄)</summary>
public class InspectionResult
{
    public long Id { get; set; }
    public DateTime InspectedAt { get; set; }
    public string ImagePath { get; set; } = "";
    public string? HeatmapPath { get; set; }
    public string Result { get; set; } = "OK";
    public double Score { get; set; }
    public double Threshold { get; set; }
    public int ElapsedMs { get; set; }
    public string Source { get; set; } = "file";
    public int? CameraId { get; set; }

    public bool IsNg => Result == "NG";
    public string ImageFileName => Path.GetFileName(ImagePath);
    public string TimeText => InspectedAt.ToString("MM-dd HH:mm:ss");
    public string SourceText => Source == "camera" ? "웹캠" : "파일";
}

/// <summary>엔진이 돌려주는 JSON 한 줄</summary>
public class EngineResponse
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("result")] public string? Result { get; set; }
    [JsonPropertyName("score")] public double Score { get; set; }
    [JsonPropertyName("threshold")] public double Threshold { get; set; }
    [JsonPropertyName("image_path")] public string? ImagePath { get; set; }
    [JsonPropertyName("heatmap_path")] public string? HeatmapPath { get; set; }
    [JsonPropertyName("elapsed_ms")] public int ElapsedMs { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("load_ms")] public int LoadMs { get; set; }
    [JsonPropertyName("device")] public string? Device { get; set; }
}

/// <summary>설정 화면에서 바꾸는 값 (settings.json 으로 저장)</summary>
public class AppSettings
{
    public string PythonPath { get; set; } = Services.ProjectPaths.Python;
    public string EngineScriptPath { get; set; } = Services.ProjectPaths.EngineScript;
    public string CapturesDir { get; set; } = Services.ProjectPaths.Captures;
    public string ResultsDir { get; set; } = Services.ProjectPaths.Results;
    public string DbPath { get; set; } = Services.ProjectPaths.Db;

    /// <summary>점수가 이 값 이상이면 NG (WPF가 판정한다)</summary>
    public double Threshold { get; set; } = 0.5;
    public int IntervalSeconds { get; set; } = 5;
    public int CameraIndex { get; set; } = 0;
    /// <summary>웹캠에 요청할 해상도 (기본 640x480 은 흐려서 1280x720 을 요청, 웹캠이 못 하면 가능한 값으로 열림)</summary>
    public int CameraWidth { get; set; } = 1280;
    public int CameraHeight { get; set; } = 720;

    /// <summary>웹캠 화면에서 가운데 정사각형만 잘라 검사한다 (모델은 너트가 꽉 찬 정사각형 사진으로 학습함)</summary>
    public bool CropEnabled { get; set; } = true;
    /// <summary>자를 정사각형 한 변 = 화면 짧은 변의 몇 %</summary>
    public double CropPercent { get; set; } = 100;
    public int EngineTimeoutSeconds { get; set; } = 60;
    public int EngineStartTimeoutSeconds { get; set; } = 400;
}
