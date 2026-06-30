using System.Collections.Concurrent;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Abstractions;

public class SprintLock : ISprintLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<string, (bool Held, DateTime Since)> _snapshots = new();

    public async Task WaitAsync(SprintId sprintId, CancellationToken ct = default)
    {
        var key = sprintId.Value;
        var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        _snapshots[key] = (true, DateTime.UtcNow);
    }

    public void Release(SprintId sprintId)
    {
        var key = sprintId.Value;
        _snapshots[key] = (false, DateTime.MinValue);
        if (_locks.TryGetValue(key, out var sem))
            sem.Release();
    }

    public (bool Held, DateTime Since) Snapshot(SprintId sprintId)
    {
        var key = sprintId.Value;
        return _snapshots.TryGetValue(key, out var snap) ? snap : (false, DateTime.MinValue);
    }
}
