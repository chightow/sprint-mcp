using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface ISprintRepository
{
    Task<Sprint?> GetActiveAsync();
    Task<Sprint?> GetByIdAsync(string sprintId);
    Task<Sprint> CreateAsync(string id);
    Task UpdateAsync(Sprint sprint);
    Task<string> GetNextIdAsync();
}
