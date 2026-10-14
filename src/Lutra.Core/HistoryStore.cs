using System.Globalization;
using System.Text.Json;

namespace Lutra.Core;

public record HistoryPoint(DateTimeOffset Time, double? Value);

/// <summary>Local, append-only daily logs. Queries keep extrema and gaps when reducing long ranges.</summary>
public sealed class HistoryStore(string directory)
{
    private DateTimeOffset lastWrite = DateTimeOffset.MinValue;
    private DateOnly lastPrune;
    public void Append(TelemetryFrame frame, int retentionDays)
    {
        if (frame.Time - lastWrite < TimeSpan.FromSeconds(5)) return;
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, frame.Time.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
        var row = JsonSerializer.Serialize(frame);
        using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        using (var writer = new StreamWriter(stream)) writer.WriteLine(row);
        lastWrite = frame.Time;
        if (lastPrune != DateOnly.FromDateTime(frame.Time.UtcDateTime))
        {
            Prune(frame.Time, retentionDays); lastPrune = DateOnly.FromDateTime(frame.Time.UtcDateTime);
        }
    }
    public void Prune(DateTimeOffset now, int retentionDays)
    {
        if (retentionDays is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(retentionDays));
        if (!Directory.Exists(directory)) return;
        var cutoff = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-retentionDays));
        foreach (var path in Directory.EnumerateFiles(directory, "*.jsonl"))
            if (DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(path), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day < cutoff)
                File.Delete(path);
    }
    public HistoryPoint[] Query(string sensorId, DateTimeOffset from, DateTimeOffset to, int maximumPoints = 1200)
    {
        if (to < from || maximumPoints < 5) throw new ArgumentException("Invalid history range or point limit.");
        if (!Directory.Exists(directory)) return [];
        var rows = new List<HistoryPoint>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.jsonl").Order())
        {
            if (!DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(path), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                || day < DateOnly.FromDateTime(from.UtcDateTime) || day > DateOnly.FromDateTime(to.UtcDateTime)) continue;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                try
                {
                    var frame = JsonSerializer.Deserialize<TelemetryFrame>(line);
                    if (frame == null || frame.Time < from || frame.Time > to) continue;
                    var value = frame.Values?.FirstOrDefault(v => v.Id == sensorId);
                    if (rows.Count > 0 && frame.Time - rows[^1].Time > TimeSpan.FromSeconds(15))
                        rows.Add(new(rows[^1].Time.AddMilliseconds(1), null));
                    rows.Add(new(frame.Time, value is { Value: { } number } && double.IsFinite(number) && value.IsFresh(frame.Time) ? number : null));
                }
                catch (JsonException) { /* An incomplete last line or corrupt row is not a measurement. */ }
            }
        }
        return Reduce(rows, maximumPoints);
    }
    public static HistoryPoint[] Reduce(IReadOnlyList<HistoryPoint> points, int maximumPoints)
    {
        if (maximumPoints < 5) throw new ArgumentOutOfRangeException(nameof(maximumPoints));
        if (points.Count <= maximumPoints) return points.ToArray();
        int buckets = Math.Max(1, maximumPoints / 5);
        var result = new List<HistoryPoint>();
        for (int i = 0; i < buckets; i++)
        {
            int start = i * points.Count / buckets, end = (i + 1) * points.Count / buckets;
            var slice = Enumerable.Range(start, end - start).Select(n => points[n]).ToArray();
            var chosen = new List<HistoryPoint> { slice[0], slice[^1] };
            var finite = slice.Where(p => p.Value.HasValue).ToArray();
            if (slice.Any(p => !p.Value.HasValue)) chosen.Add(slice.First(p => !p.Value.HasValue));
            if (finite.Length > 0)
            {
                chosen.Add(finite.MaxBy(p => p.Value)!);
                chosen.Add(finite.MinBy(p => p.Value)!);
            }
            result.AddRange(chosen.Distinct().OrderBy(p => p.Time));
        }
        return result.Distinct().OrderBy(p => p.Time).ToArray();
    }
}
