using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace VisionInspector.Services;

/// <summary>웹캠 미리보기와 한 프레임 저장 (OpenCvSharp)</summary>
public sealed class CameraService : IDisposable
{
    private VideoCapture? _cap;
    private Task? _loop;
    private volatile bool _running;
    private Mat? _last;
    private readonly object _lock = new();

    public bool IsRunning => _running;

    private volatile bool _cropEnabled = true;
    private double _cropPercent = 100;

    /// <summary>가운데 정사각형 자르기 설정. 미리보기에 주황 네모로 보이고, 저장할 때 그 안쪽만 남긴다.</summary>
    public void ConfigureCrop(bool enabled, double percent)
    {
        _cropEnabled = enabled;
        _cropPercent = Math.Clamp(percent, 30, 100);
    }

    private Rect GetCropRect(int width, int height)
    {
        var side = (int)(Math.Min(width, height) * _cropPercent / 100.0);
        side = Math.Max(32, side);
        return new Rect((width - side) / 2, (height - side) / 2, side, side);
    }

    /// <summary>미리보기 프레임이 올 때마다 발생 (UI 스레드가 아닐 수 있음, Freeze 됨)</summary>
    public event Action<BitmapSource>? FrameReady;

    /// <summary>웹캠이 실제로 열린 해상도</summary>
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }

    public bool Start(int index, int width = 1280, int height = 720)
    {
        Stop();
        try
        {
            var cap = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
            if (!cap.IsOpened())
            {
                cap.Dispose();
                return false;
            }
            // 높은 해상도는 보통 MJPG 로만 나온다. 웹캠이 지원하지 않으면 무시되고 기본값으로 열린다.
            cap.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
            cap.Set(VideoCaptureProperties.FrameWidth, width);
            cap.Set(VideoCaptureProperties.FrameHeight, height);
            FrameWidth = (int)cap.Get(VideoCaptureProperties.FrameWidth);
            FrameHeight = (int)cap.Get(VideoCaptureProperties.FrameHeight);
            _cap = cap;
        }
        catch
        {
            return false;
        }
        _running = true;
        _loop = Task.Run(Loop);
        return true;
    }

    private void Loop()
    {
        using var frame = new Mat();
        while (_running)
        {
            try
            {
                if (_cap == null || !_cap.Read(frame) || frame.Empty())
                {
                    Thread.Sleep(30);
                    continue;
                }
                lock (_lock)
                {
                    _last?.Dispose();
                    _last = frame.Clone();
                }
                BitmapSource bmp;
                if (_cropEnabled)
                {
                    // 검사에 쓰일 영역을 주황 네모로 보여 준다
                    using var view = frame.Clone();
                    Cv2.Rectangle(view, GetCropRect(view.Width, view.Height), new Scalar(11, 158, 245), 3);
                    bmp = BitmapSourceConverter.ToBitmapSource(view);
                }
                else
                {
                    bmp = BitmapSourceConverter.ToBitmapSource(frame);
                }
                bmp.Freeze();
                FrameReady?.Invoke(bmp);
            }
            catch
            {
                Thread.Sleep(50);
            }
            Thread.Sleep(15);
        }
    }

    /// <summary>가장 최근 프레임을 PNG 로 저장한다. 프레임이 없으면 false.</summary>
    public bool SaveFrame(string path)
    {
        lock (_lock)
        {
            if (_last == null || _last.Empty()) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!_cropEnabled) return Cv2.ImWrite(path, _last);
            using var roi = new Mat(_last, GetCropRect(_last.Width, _last.Height));
            return Cv2.ImWrite(path, roi);
        }
    }

    public void Stop()
    {
        _running = false;
        try
        {
            _loop?.Wait(1000);
        }
        catch
        {
            // 무시
        }
        _loop = null;
        _cap?.Dispose();
        _cap = null;
        lock (_lock)
        {
            _last?.Dispose();
            _last = null;
        }
    }

    public void Dispose() => Stop();
}
