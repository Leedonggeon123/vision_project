using VisionInspector.Models;

namespace VisionInspector.Services;

/// <summary>사진 한 장을 엔진에 보내 검사하고 판정·저장까지 묶어서 처리한다.</summary>
public class InspectionService
{
    private readonly EngineService _engine;
    private readonly DbService _db;
    private readonly AppSettings _settings;

    public InspectionService(EngineService engine, DbService db, AppSettings settings)
    {
        _engine = engine;
        _db = db;
        _settings = settings;
    }

    /// <summary>검사 한 건이 저장될 때마다 발생 (호출한 스레드에서)</summary>
    public event Action<InspectionResult>? ResultAdded;

    public async Task<InspectionResult> InspectAsync(string imagePath, string source)
    {
        var resp = await _engine.InspectAsync(imagePath, _settings.EngineTimeoutSeconds);
        if (!resp.Ok)
            throw new InvalidOperationException(resp.Error ?? "엔진이 오류를 돌려주었습니다.");

        // 설계: OK/NG 판정은 WPF 가 한다. (설정 화면의 기준값과 점수를 비교)
        var threshold = _settings.Threshold;
        var result = new InspectionResult
        {
            InspectedAt = DateTime.Now,
            ImagePath = imagePath,
            HeatmapPath = resp.HeatmapPath,
            Result = resp.Score >= threshold ? "NG" : "OK",
            Score = resp.Score,
            Threshold = threshold,
            ElapsedMs = resp.ElapsedMs,
            Source = source,
            CameraId = source == "camera" ? _settings.CameraIndex : null,
        };
        result.Id = _db.Insert(result);
        ResultAdded?.Invoke(result);
        return result;
    }
}
