using System.Globalization;
using Microsoft.Data.Sqlite;
using VisionInspector.Models;

namespace VisionInspector.Services;

/// <summary>검사 이력을 SQLite 에 저장하고 읽는다. (이미지는 파일, DB에는 경로만)</summary>
public class DbService
{
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";
    private readonly string _connectionString;

    public DbService(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
    }

    private SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    public void Init()
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS inspections (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                inspected_at TEXT    NOT NULL,
                image_path   TEXT    NOT NULL,
                heatmap_path TEXT,
                result       TEXT    NOT NULL,
                score        REAL    NOT NULL,
                threshold    REAL    NOT NULL,
                elapsed_ms   INTEGER NOT NULL,
                source       TEXT    NOT NULL,
                camera_id    INTEGER
            );
            CREATE INDEX IF NOT EXISTS idx_inspections_time ON inspections(inspected_at);
            """;
        cmd.ExecuteNonQuery();
    }

    public long Insert(InspectionResult r)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            INSERT INTO inspections
                (inspected_at, image_path, heatmap_path, result, score, threshold, elapsed_ms, source, camera_id)
            VALUES ($t, $img, $heat, $res, $score, $thr, $ms, $src, $cam);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$t", r.InspectedAt.ToString(TimeFormat, CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$img", r.ImagePath);
        cmd.Parameters.AddWithValue("$heat", (object?)r.HeatmapPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$res", r.Result);
        cmd.Parameters.AddWithValue("$score", r.Score);
        cmd.Parameters.AddWithValue("$thr", r.Threshold);
        cmd.Parameters.AddWithValue("$ms", r.ElapsedMs);
        cmd.Parameters.AddWithValue("$src", r.Source);
        cmd.Parameters.AddWithValue("$cam", (object?)r.CameraId ?? DBNull.Value);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public List<InspectionResult> Query(DateTime? from, DateTime? to, string? result, int limit = 2000)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        var where = new List<string>();
        if (from != null)
        {
            where.Add("inspected_at >= $from");
            cmd.Parameters.AddWithValue("$from", from.Value.Date.ToString(TimeFormat, CultureInfo.InvariantCulture));
        }
        if (to != null)
        {
            where.Add("inspected_at < $to");
            cmd.Parameters.AddWithValue("$to", to.Value.Date.AddDays(1).ToString(TimeFormat, CultureInfo.InvariantCulture));
        }
        if (!string.IsNullOrEmpty(result) && result != "전체")
        {
            where.Add("result = $res");
            cmd.Parameters.AddWithValue("$res", result);
        }
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        cmd.CommandText = $"""
            SELECT id, inspected_at, image_path, heatmap_path, result, score, threshold, elapsed_ms, source, camera_id
            FROM inspections {whereSql}
            ORDER BY inspected_at DESC, id DESC
            LIMIT {limit};
            """;
        var list = new List<InspectionResult>();
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(Read(rd));
        return list;
    }

    private static InspectionResult Read(SqliteDataReader rd) => new()
    {
        Id = rd.GetInt64(0),
        InspectedAt = DateTime.ParseExact(rd.GetString(1), TimeFormat, CultureInfo.InvariantCulture),
        ImagePath = rd.GetString(2),
        HeatmapPath = rd.IsDBNull(3) ? null : rd.GetString(3),
        Result = rd.GetString(4),
        Score = rd.GetDouble(5),
        Threshold = rd.GetDouble(6),
        ElapsedMs = rd.GetInt32(7),
        Source = rd.GetString(8),
        CameraId = rd.IsDBNull(9) ? null : rd.GetInt32(9),
    };

    /// <summary>기간 요약: 전체/OK/NG 건수와 평균 검사 시간. from 이 null 이면 전체 기간.</summary>
    public (int Total, int Ok, int Ng, double AvgMs) Summary(DateTime? from)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        var where = "";
        if (from != null)
        {
            where = "WHERE inspected_at >= $from";
            cmd.Parameters.AddWithValue("$from", from.Value.Date.ToString(TimeFormat, CultureInfo.InvariantCulture));
        }
        cmd.CommandText = $"""
            SELECT COUNT(*),
                   COALESCE(SUM(CASE WHEN result = 'OK' THEN 1 ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN result = 'NG' THEN 1 ELSE 0 END), 0),
                   COALESCE(AVG(elapsed_ms), 0)
            FROM inspections {where};
            """;
        using var rd = cmd.ExecuteReader();
        rd.Read();
        return (rd.GetInt32(0), rd.GetInt32(1), rd.GetInt32(2), rd.GetDouble(3));
    }

    /// <summary>최근 N일(오늘 포함) 날짜별 OK/NG 건수. 검사가 없는 날은 0.</summary>
    public List<(DateTime Day, int Ok, int Ng)> DailyCounts(int days)
    {
        var start = DateTime.Today.AddDays(-(days - 1));
        var map = new Dictionary<DateTime, (int Ok, int Ng)>();
        using (var c = Open())
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = """
                SELECT substr(inspected_at, 1, 10) AS d,
                       SUM(CASE WHEN result = 'OK' THEN 1 ELSE 0 END),
                       SUM(CASE WHEN result = 'NG' THEN 1 ELSE 0 END)
                FROM inspections
                WHERE inspected_at >= $from
                GROUP BY d;
                """;
            cmd.Parameters.AddWithValue("$from", start.ToString(TimeFormat, CultureInfo.InvariantCulture));
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var day = DateTime.ParseExact(rd.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                map[day] = (rd.GetInt32(1), rd.GetInt32(2));
            }
        }
        var list = new List<(DateTime, int, int)>();
        for (var i = 0; i < days; i++)
        {
            var d = start.AddDays(i);
            map.TryGetValue(d, out var v);
            list.Add((d, v.Ok, v.Ng));
        }
        return list;
    }

    /// <summary>최근 N건의 점수 (오래된 것부터)</summary>
    public List<double> RecentScores(int n)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT score FROM inspections ORDER BY inspected_at DESC, id DESC LIMIT $n;";
        cmd.Parameters.AddWithValue("$n", n);
        var list = new List<double>();
        using var rd = cmd.ExecuteReader();
        while (rd.Read()) list.Add(rd.GetDouble(0));
        list.Reverse();
        return list;
    }
}
