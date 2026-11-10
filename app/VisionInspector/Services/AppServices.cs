using VisionInspector.Models;

namespace VisionInspector.Services;

/// <summary>화면들이 함께 쓰는 서비스 모음 (프로그램이 시작할 때 한 번 만든다)</summary>
public static class AppServices
{
    public static SettingsService SettingsService { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;
    public static DbService Db { get; private set; } = null!;
    public static EngineService Engine { get; private set; } = null!;
    public static CameraService Camera { get; private set; } = null!;
    public static InspectionService Inspection { get; private set; } = null!;

    public static void Initialize()
    {
        SettingsService = new SettingsService();
        Settings = SettingsService.Load();
        Directory.CreateDirectory(Settings.CapturesDir);
        Directory.CreateDirectory(Settings.ResultsDir);

        Db = new DbService(Settings.DbPath);
        Db.Init();

        Engine = new EngineService();
        Camera = new CameraService();
        Camera.ConfigureCrop(Settings.CropEnabled, Settings.CropPercent);
        Inspection = new InspectionService(Engine, Db, Settings);
    }

    public static void Shutdown()
    {
        Camera?.Dispose();
        Engine?.Dispose();
    }
}
