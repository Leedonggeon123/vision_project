namespace VisionInspector.Services;

/// <summary>프로젝트 폴더(vision_project)를 찾아서 기본 경로를 알려 준다.</summary>
public static class ProjectPaths
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        // 실행 파일 위치에서 위로 올라가며 engine\infer_engine.py 가 있는 폴더를 찾는다.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "engine", "infer_engine.py")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    public static string Python => Path.Combine(Root, ".venv_engine", "Scripts", "python.exe");
    public static string EngineScript => Path.Combine(Root, "engine", "infer_engine.py");
    public static string Captures => Path.Combine(Root, "captures");
    public static string Results => Path.Combine(Root, "results");
    public static string Db => Path.Combine(Root, "inspection.db");
    public static string SettingsFile => Path.Combine(Root, "app", "settings.json");
}
