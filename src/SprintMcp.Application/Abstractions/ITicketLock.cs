namespace SprintMcp.Application.Abstractions;

public interface ITicketLock
{
    Task WaitAsync(CancellationToken ct = default);
    void Release();
}
