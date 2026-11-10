using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VisionInspector.Converters;

/// <summary>화면 전체에서 쓰는 색 (OK 초록 / NG 빨강 / 강조 주황)</summary>
public static class Theme
{
    public static readonly Brush OkBrush = Make("#22C55E");
    public static readonly Brush NgBrush = Make("#EF4444");
    public static readonly Brush IdleBrush = Make("#4B5563");
    public static readonly Brush WarnBrush = Make("#F59E0B");

    private static Brush Make(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}

/// <summary>파일 경로 → 이미지. 파일을 잠그지 않도록 한 번에 읽어 들인다.</summary>
public class PathToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Load(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    public static BitmapImage? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>"OK"/"NG" → 초록/빨강</summary>
public class ResultToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string) switch
        {
            "OK" => Theme.OkBrush,
            "NG" => Theme.NgBrush,
            _ => Theme.IdleBrush,
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>값이 비어 있으면 숨김. ConverterParameter=inv 이면 반대 (비어 있을 때만 보임)</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var empty = value == null || (value is string s && string.IsNullOrEmpty(s));
        var inverse = parameter as string == "inv";
        return empty != inverse ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true ↔ false 뒤집기 (예: 폴더 검사 중에는 웹캠 버튼을 막는다)</summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
