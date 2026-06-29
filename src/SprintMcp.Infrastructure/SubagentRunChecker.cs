using System.Globalization;
using System.Text.Json;
using SprintMcp.Application.Abstractions;

namespace SprintMcp.Infrastructure;

public class SubagentRunChecker : ISubagentRunChecker
{
    private static readonly string[] LogPaths =
    [
        ".canon/subagent-runs.jsonl",
        ".claude/subagent-runs.jsonl",
        ".opencode/subagent-runs.jsonl"
    ];

    private const int WindowSec = 600;

    public bool CheckRun(long epoch, string projectRoot)
    {
        foreach (var rel in LogPaths)
        {
            if (CheckFile(Path.Combine(projectRoot, rel), epoch))
                return true;
        }
        return false;
    }

    private static bool CheckFile(string path, long runEpoch)
    {
        if (!File.Exists(path))
            return false;

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var ts = doc.RootElement.TryGetProperty("ts", out var tsProp) ? tsProp.GetString() : null;
                if (ts is null) continue;

                if (TryParseTimestamp(ts) is { } entryEpoch)
                {
                    var diff = entryEpoch - runEpoch;
                    if (diff < 0) diff = -diff;
                    if (diff <= WindowSec)
                        return true;
                }
            }
            catch
            {
                // skip malformed lines
            }
        }
        return false;
    }

    private static long? TryParseTimestamp(string ts)
    {
        var normalized = ts.EndsWith('Z') ? ts[..^1] + "+00:00" : ts;

        string[] formats =
        [
            "yyyy-MM-ddTHH:mm:ss.fffffffK",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:ss"
        ];

        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(normalized, fmt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var dt))
                return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }
        return null;
    }
}
