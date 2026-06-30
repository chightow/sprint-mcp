using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface ISprintHandoffRepository
{
    Task<SprintHandoff?> GetBySprintIdAsync(SprintId sprintId, CancellationToken ct = default);
    Task UpsertAsync(SprintHandoff handoff, CancellationToken ct = default);
}
