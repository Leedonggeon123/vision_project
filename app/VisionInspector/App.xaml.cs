using System.Windows;
using VisionInspector.Services;

namespace VisionInspector;

public partial class App : Application
{
    private static readonly string CrashLog = Path.Combine(ProjectPaths.Root, "app", "crash.log");

    public App()
    {
        // 어디선가 예외가 나면 조용히 죽지 않고 파일에 남기고 알려 준다
        DispatcherUnhandledException += (_, e) =>
        {
            Log(e.Exception);
            MessageBox.Show($"오류가 발생했습니다.\n\n{e.Exception.Message}\n\n자세한 내용: {CrashLog}",
                "AI 비전 검사", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log(e.Exception);
            e.SetObserved();
        };
    }

    private static void Log(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLog)!);
            File.AppendAllText(CrashLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // 로그를 못 남겨도 프로그램은 계속
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // 창이 만들어지기 전에 서비스(DB, 엔진, 카메라)부터 준비한다
        AppServices.Initialize();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppServices.Shutdown();   // 엔진에 quit 를 보내고 웹캠을 닫는다
        base.OnExit(e);
    }
}
