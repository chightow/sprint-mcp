using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface IActiveTaskRepository
{
    Task<List<ActiveTask>> GetBySprintIdAsync(SprintId sprintId, CancellationToken ct = default);
    Task AddAsync(ActiveTask task, CancellationToken ct = default);
    Task<bool> DeleteBySprintIdAndIdAsync(SprintId sprintId, int id, CancellationToken ct = default);
}
