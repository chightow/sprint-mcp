using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SubagentRunChecker> _logger;

    public SubagentRunChecker(ILogger<SubagentRunChecker> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CheckRunAsync(long epoch, string projectRoot, CancellationToken ct = default)
    {
        foreach (var rel in LogPaths)
        {
            if (await CheckFileAsync(Path.Combine(projectRoot, rel), epoch, ct))
                return true;
        }
        _logger.LogWarning("Subagent run {Epoch} not found in any log path under {Root}", epoch, projectRoot);
        return false;
    }

    private async Task<bool> CheckFileAsync(string path, long runEpoch, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            return false;

        var lines = await File.ReadAllLinesAsync(path, ct);
        foreach (var line in lines)
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
            catch (Exception ex)
            {
                _logger.LogTrace("Skipping malformed subagent run line: {Error}", ex.Message);
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
