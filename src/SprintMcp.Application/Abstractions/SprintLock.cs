using System.Collections.Concurrent;

namespace SprintMcp.Application.Abstractions;

public class SprintLock : ISprintLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ConcurrentDictionary<string, (bool Held, DateTime Since)> _snapshots = new();

    public async Task WaitAsync(string sprintId, CancellationToken ct = default)
    {
        var sem = _locks.GetOrAdd(sprintId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        _snapshots[sprintId] = (true, DateTime.UtcNow);
    }

    public void Release(string sprintId)
    {
        _snapshots[sprintId] = (false, DateTime.MinValue);
        if (_locks.TryGetValue(sprintId, out var sem))
            sem.Release();
    }

    public (bool Held, DateTime Since) Snapshot(string sprintId)
    {
        return _snapshots.TryGetValue(sprintId, out var snap) ? snap : (false, DateTime.MinValue);
    }
}
