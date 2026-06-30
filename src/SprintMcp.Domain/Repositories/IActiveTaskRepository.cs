using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IActiveTaskRepository
{
    Task<List<ActiveTask>> GetBySprintIdAsync(string sprintId, CancellationToken ct = default);
    Task AddAsync(ActiveTask task, CancellationToken ct = default);
    Task<bool> DeleteBySprintIdAndIdAsync(string sprintId, int id, CancellationToken ct = default);
}
