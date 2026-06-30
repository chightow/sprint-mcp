namespace SprintMcp.Application.Abstractions;

public interface ISprintLock
{
    Task WaitAsync(CancellationToken ct = default);
    void Release();
    (bool Held, DateTime Since) Snapshot();
}
