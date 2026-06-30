using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface ISprintRepository
{
    Task<Sprint?> GetActiveAsync(CancellationToken ct = default);
    Task<Sprint?> GetByIdAsync(string sprintId, CancellationToken ct = default);
    Task<Sprint> CreateAsync(string id, CancellationToken ct = default);
    Task UpdateAsync(Sprint sprint, CancellationToken ct = default);
    Task<string> GetNextIdAsync(CancellationToken ct = default);
}
