using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
using VisionInspector.Services;

namespace VisionInspector.ViewModels;

public record PeriodOption(string Label, int Days);

public partial class DashboardViewModel : ObservableObject, IActivatable
{
    private static readonly SKColor Green = SKColor.Parse("#22C55E");
    private static readonly SKColor Red = SKColor.Parse("#EF4444");
    private static readonly SKColor Orange = SKColor.Parse("#F59E0B");
    private static readonly SKColor Light = SKColor.Parse("#D1D5DB");
    private static readonly SKColor Grid = new(255, 255, 255, 28);

    public PeriodOption[] Periods { get; } =
    [
        new("오늘", 1), new("최근 7일", 7), new("최근 30일", 30), new("전체", 0),
    ];

    [ObservableProperty] private PeriodOption selectedPeriod;
    [ObservableProperty] private string totalText = "0";
    [ObservableProperty] private string okText = "0";
    [ObservableProperty] private string ngText = "0";
    [ObservableProperty] private string ngRateText = "-";
    [ObservableProperty] private string avgMsText = "-";

    [ObservableProperty] private ISeries[] pieSeries = [];
    [ObservableProperty] private ISeries[] dailySeries = [];
    [ObservableProperty] private ICartesianAxis[] dailyXAxes = [];
    [ObservableProperty] private ICartesianAxis[] dailyYAxes = [];
    [ObservableProperty] private ISeries[] scoreSeries = [];
    [ObservableProperty] private ICartesianAxis[] scoreXAxes = [];
    [ObservableProperty] private ICartesianAxis[] scoreYAxes = [];

    public SolidColorPaint LegendPaint { get; } = new(Light);

    public DashboardViewModel()
    {
        selectedPeriod = Periods[1];
    }

    public void OnActivated() => Refresh();

    partial void OnSelectedPeriodChanged(PeriodOption value) => Refresh();

    private void Refresh()
    {
        var db = AppServices.Db;
        var days = SelectedPeriod.Days;
        DateTime? from = days == 0 ? null : DateTime.Today.AddDays(-(days - 1));

        // ----- 숫자 카드 -----
        var s = db.Summary(from);
        TotalText = s.Total.ToString();
        OkText = s.Ok.ToString();
        NgText = s.Ng.ToString();
        NgRateText = s.Total == 0 ? "-" : $"{100.0 * s.Ng / s.Total:F1}%";
        AvgMsText = s.Total == 0 ? "-" : $"{s.AvgMs:F0} ms";

        // ----- 도넛: OK / NG 비율 -----
        PieSeries =
        [
            new PieSeries<int> { Values = [s.Ok], Name = "OK", Fill = new SolidColorPaint(Green), InnerRadius = 40 },
            new PieSeries<int> { Values = [s.Ng], Name = "NG", Fill = new SolidColorPaint(Red), InnerRadius = 40 },
        ];

        // ----- 날짜별 막대 -----
        var chartDays = days is 0 or 1 ? 7 : days;
        var daily = db.DailyCounts(chartDays);
        DailySeries =
        [
            new StackedColumnSeries<int> { Values = daily.Select(d => d.Ok).ToArray(), Name = "OK", Fill = new SolidColorPaint(Green) },
            new StackedColumnSeries<int> { Values = daily.Select(d => d.Ng).ToArray(), Name = "NG", Fill = new SolidColorPaint(Red) },
        ];
        DailyXAxes =
        [
            new Axis
            {
                Labels = daily.Select(d => d.Day.ToString("MM-dd")).ToArray(),
                LabelsPaint = new SolidColorPaint(Light),
                TextSize = 12,
            },
        ];
        DailyYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MinStep = 1,
                LabelsPaint = new SolidColorPaint(Light),
                SeparatorsPaint = new SolidColorPaint(Grid),
                TextSize = 12,
            },
        ];

        // ----- 최근 점수 추이 + 기준값 선 -----
        var scores = db.RecentScores(50);
        var thr = AppServices.Settings.Threshold;
        ScoreSeries =
        [
            new LineSeries<double>
            {
                Values = scores.ToArray(),
                Name = "점수",
                Fill = null,
                GeometrySize = 7,
                LineSmoothness = 0,
                Stroke = new SolidColorPaint(Orange) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(Orange) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(Orange),
            },
            new LineSeries<double>
            {
                Values = Enumerable.Repeat(thr, Math.Max(scores.Count, 2)).ToArray(),
                Name = $"기준값 {thr:F2}",
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                Stroke = new SolidColorPaint(Red) { StrokeThickness = 1.5f, PathEffect = new DashEffect([6f, 6f]) },
            },
        ];
        ScoreXAxes =
        [
            new Axis { LabelsPaint = new SolidColorPaint(Light), TextSize = 12, MinStep = 1 },
        ];
        ScoreYAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 1.05,
                LabelsPaint = new SolidColorPaint(Light),
                SeparatorsPaint = new SolidColorPaint(Grid),
                TextSize = 12,
            },
        ];
    }
}
