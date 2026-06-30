namespace SprintMcp.Application.Abstractions;

public class SprintLock : ISprintLock
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private DateTime _lockAcquiredAt = DateTime.MinValue;

    public async Task WaitAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        _lockAcquiredAt = DateTime.UtcNow;
    }

    public void Release() => _mutex.Release();

    public (bool Held, DateTime Since) Snapshot()
    {
        var held = _mutex.CurrentCount == 0;
        return (held, _lockAcquiredAt);
    }
}
