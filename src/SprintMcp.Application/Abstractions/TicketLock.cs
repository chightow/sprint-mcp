namespace SprintMcp.Application.Abstractions;

public class TicketLock : ITicketLock
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public Task WaitAsync(CancellationToken ct = default) => _mutex.WaitAsync(ct);
    public void Release() => _mutex.Release();
}
