namespace SprintMcp.Application.Abstractions;

public class SprintLock : ISprintLock
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly object _snapshotLock = new();
    private bool _held;
    private DateTime _lockAcquiredAt = DateTime.MinValue;

    public async Task WaitAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        lock (_snapshotLock)
        {
            _held = true;
            _lockAcquiredAt = DateTime.UtcNow;
        }
    }

    public void Release()
    {
        lock (_snapshotLock)
        {
            _held = false;
            _lockAcquiredAt = DateTime.MinValue;
        }
        _mutex.Release();
    }

    public (bool Held, DateTime Since) Snapshot()
    {
        lock (_snapshotLock)
        {
            return (_held, _lockAcquiredAt);
        }
    }
}
