using System.Text.Json;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Application.Services;

public class IdempotencyService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private int _purgeCounter;

    private readonly IIdempotencyRepository _repo;
    private readonly TimeProvider _timeProvider;

    public IdempotencyService(IIdempotencyRepository repo, TimeProvider timeProvider)
    {
        _repo = repo;
        _timeProvider = timeProvider;
    }

    public async Task<ToolResult?> CheckAsync(string? key, CancellationToken ct = default)
    {
        if (key is null) return null;

        if (Interlocked.Increment(ref _purgeCounter) % 50 == 0)
            await _repo.PurgeExpiredAsync(Ttl, ct);

        var cached = await _repo.GetAsync(key, ct);
        if (cached is null) return null;
        if (_timeProvider.GetUtcNow().UtcDateTime - cached.CreatedAt >= Ttl)
        {
            await _repo.DeleteAsync(key, ct);
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ToolResult>(cached.ResultJson);
        }
        catch (JsonException)
        {
            await _repo.DeleteAsync(key, ct);
            return null;
        }
    }

    public async Task StoreAsync(string? key, ToolResult result, CancellationToken ct = default)
    {
        if (key is null) return;
        var json = JsonSerializer.Serialize(result);
        await _repo.StoreAsync(key, json, ct);
    }
}
