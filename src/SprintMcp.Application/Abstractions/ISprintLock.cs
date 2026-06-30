namespace SprintMcp.Application.Abstractions;

public interface ISprintLock
{
    Task WaitAsync(string sprintId, CancellationToken ct = default);
    void Release(string sprintId);
    (bool Held, DateTime Since) Snapshot(string sprintId);
}
