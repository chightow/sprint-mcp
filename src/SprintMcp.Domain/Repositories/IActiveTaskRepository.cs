using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IActiveTaskRepository
{
    Task<List<ActiveTask>> GetBySprintIdAsync(string sprintId);
    Task AddAsync(ActiveTask task);
    Task DeleteAllBySprintIdAsync(string sprintId);
}
