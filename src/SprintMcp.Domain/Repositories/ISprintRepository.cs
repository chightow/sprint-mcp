using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface ISprintRepository
{
    Task<Sprint?> GetActiveAsync(CancellationToken ct = default);
    Task<List<Sprint>> GetAllActiveAsync(CancellationToken ct = default);
    Task<Sprint?> GetByIdAsync(SprintId sprintId, CancellationToken ct = default);
    Task<Sprint> CreateAsync(SprintId id, CancellationToken ct = default);
    Task<Sprint> CreateNextAsync(CancellationToken ct = default);
    Task UpdateAsync(Sprint sprint, CancellationToken ct = default);
}
