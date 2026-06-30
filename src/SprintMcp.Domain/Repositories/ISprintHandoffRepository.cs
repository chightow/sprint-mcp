using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface ISprintHandoffRepository
{
    Task<SprintHandoff?> GetBySprintIdAsync(string sprintId, CancellationToken ct = default);
    Task UpsertAsync(SprintHandoff handoff, CancellationToken ct = default);
}
