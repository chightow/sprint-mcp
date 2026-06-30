using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Abstractions;

public interface ISprintLock
{
    Task WaitAsync(SprintId sprintId, CancellationToken ct = default);
    void Release(SprintId sprintId);
    (bool Held, DateTime Since) Snapshot(SprintId sprintId);
}
