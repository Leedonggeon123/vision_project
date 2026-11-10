using System.Text.Json;
using VisionInspector.Models;

namespace VisionInspector.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(ProjectPaths.SettingsFile))
            {
                var json = File.ReadAllText(ProjectPaths.SettingsFile);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s != null) return s;
            }
        }
        catch
        {
            // 설정 파일이 깨졌으면 기본값으로 시작한다
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProjectPaths.SettingsFile)!);
        File.WriteAllText(ProjectPaths.SettingsFile, JsonSerializer.Serialize(settings, Options));
    }
}
