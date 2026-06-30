using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IActiveTaskRepository
{
    Task<List<ActiveTask>> GetBySprintIdAsync(string sprintId, CancellationToken ct = default);
    Task<ActiveTask?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(ActiveTask task, CancellationToken ct = default);
    Task<bool> DeleteByIdAsync(int id, CancellationToken ct = default);
    Task DeleteAllBySprintIdAsync(string sprintId, CancellationToken ct = default);
}
