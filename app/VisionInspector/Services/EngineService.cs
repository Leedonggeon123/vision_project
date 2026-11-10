using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using VisionInspector.Models;

namespace VisionInspector.Services;

public enum EngineState { Stopped, Starting, Ready, Error }

/// <summary>
/// Python 검사 엔진(infer_engine.py --serve)을 켜 두고 표준 입출력으로 주고받는다.
/// 사진 경로 한 줄을 보내면 JSON 한 줄이 돌아온다. 한 번에 한 장씩만 처리한다.
/// </summary>
public sealed class EngineService : IDisposable
{
    private Process? _proc;
    private Channel<string> _lines = Channel.CreateUnbounded<string>();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _lastStderr = "";

    public EngineState State { get; private set; } = EngineState.Stopped;
    public string StateMessage { get; private set; } = "엔진이 꺼져 있음";
    public string Device { get; private set; } = "";

    public event Action<EngineState, string>? StateChanged;

    private void SetState(EngineState state, string message)
    {
        State = state;
        StateMessage = message;
        StateChanged?.Invoke(state, message);
    }

    public async Task StartAsync(AppSettings s, CancellationToken ct = default)
    {
        Stop();

        if (!File.Exists(s.PythonPath))
        {
            SetState(EngineState.Error, $"Python을 찾을 수 없음: {s.PythonPath}");
            return;
        }
        if (!File.Exists(s.EngineScriptPath))
        {
            SetState(EngineState.Error, $"엔진 스크립트를 찾을 수 없음: {s.EngineScriptPath}");
            return;
        }

        SetState(EngineState.Starting, "엔진 준비 중... (처음에는 오래 걸릴 수 있음)");
        _lines = Channel.CreateUnbounded<string>();
        _lastStderr = "";
        Directory.CreateDirectory(s.ResultsDir);

        var psi = new ProcessStartInfo
        {
            FileName = s.PythonPath,
            WorkingDirectory = Path.GetDirectoryName(s.EngineScriptPath)!,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        psi.ArgumentList.Add("-I");
        psi.ArgumentList.Add(s.EngineScriptPath);
        psi.ArgumentList.Add("--serve");
        psi.ArgumentList.Add("--out-dir");
        psi.ArgumentList.Add(s.ResultsDir);
        psi.Environment["PYTHONUNBUFFERED"] = "1";

        Process proc;
        try
        {
            proc = Process.Start(psi) ?? throw new InvalidOperationException("프로세스를 시작하지 못했습니다");
        }
        catch (Exception ex)
        {
            SetState(EngineState.Error, $"엔진 실행 실패: {ex.Message}");
            return;
        }
        _proc = proc;
        var writer = _lines.Writer;

        // 에러 출력(stderr)은 읽어서 버려야 한다. 안 읽으면 버퍼가 차서 엔진이 멈춘다.
        proc.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data)) _lastStderr = e.Data;
        };
        proc.BeginErrorReadLine();

        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await proc.StandardOutput.ReadLineAsync()) != null)
                    writer.TryWrite(line);
            }
            catch
            {
                // 프로세스가 끝나면 읽기가 실패할 수 있다
            }
            finally
            {
                writer.TryComplete();
            }
        });

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(s.EngineStartTimeoutSeconds));
            var line = await _lines.Reader.ReadAsync(cts.Token);
            var r = JsonSerializer.Deserialize<EngineResponse>(line);
            if (r?.Status == "ready")
            {
                Device = r.Device ?? "";
                SetState(EngineState.Ready, $"엔진 준비 완료 ({Device}, 시작 {r.LoadMs / 1000.0:F1}초)");
            }
            else
            {
                SetState(EngineState.Error, r?.Error ?? line);
            }
        }
        catch (OperationCanceledException)
        {
            Kill();
            SetState(EngineState.Error, "엔진 시작 시간 초과");
        }
        catch (ChannelClosedException)
        {
            SetState(EngineState.Error, $"엔진이 종료됨: {_lastStderr}");
        }
        catch (Exception ex)
        {
            SetState(EngineState.Error, $"엔진 시작 오류: {ex.Message}");
        }
    }

    /// <summary>사진 한 장을 검사한다. (엔진이 준비된 상태에서만)</summary>
    public async Task<EngineResponse> InspectAsync(string imagePath, int timeoutSeconds, CancellationToken ct = default)
    {
        if (State != EngineState.Ready || _proc == null)
            throw new InvalidOperationException("엔진이 준비되지 않았습니다.");

        await _gate.WaitAsync(ct);
        try
        {
            await _proc.StandardInput.WriteLineAsync(imagePath);
            await _proc.StandardInput.FlushAsync(ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            var line = await _lines.Reader.ReadAsync(cts.Token);
            return JsonSerializer.Deserialize<EngineResponse>(line)
                   ?? throw new InvalidOperationException("엔진 응답을 읽을 수 없습니다.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Kill();
            SetState(EngineState.Error, "엔진 응답 시간 초과 (다시 시작해 주세요)");
            throw new TimeoutException("엔진 응답 시간 초과");
        }
        catch (ChannelClosedException)
        {
            SetState(EngineState.Error, $"엔진이 종료됨: {_lastStderr}");
            throw new InvalidOperationException("엔진이 종료되었습니다.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Kill()
    {
        var p = _proc;
        _proc = null;
        if (p == null) return;
        try
        {
            if (!p.HasExited) p.Kill(true);
        }
        catch
        {
            // 이미 끝났으면 무시
        }
        finally
        {
            p.Dispose();
        }
    }

    public void Stop()
    {
        var p = _proc;
        _proc = null;
        if (p == null) return;
        try
        {
            if (!p.HasExited)
            {
                try
                {
                    p.StandardInput.WriteLine("quit");
                    p.StandardInput.Flush();
                }
                catch
                {
                    // 입력이 이미 닫혔을 수 있다
                }
                if (!p.WaitForExit(2000)) p.Kill(true);
            }
        }
        catch
        {
            // 무시
        }
        finally
        {
            p.Dispose();
        }
        State = EngineState.Stopped;
        StateMessage = "엔진이 꺼져 있음";
    }

    public void Dispose() => Stop();
}
