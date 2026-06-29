using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface ISprintHandoffRepository
{
    Task<SprintHandoff?> GetBySprintIdAsync(string sprintId);
    Task UpdateAsync(SprintHandoff handoff);
}
