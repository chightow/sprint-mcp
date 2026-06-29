using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class ActiveTaskRepository(AppDbContext db) : IActiveTaskRepository
{
    public async Task<List<ActiveTask>> GetBySprintIdAsync(string sprintId)
    {
        return await db.ActiveTasks
            .Where(t => t.SprintId == sprintId)
            .OrderBy(t => t.Ordinal)
            .ToListAsync();
    }

    public async Task AddAsync(ActiveTask task)
    {
        db.ActiveTasks.Add(task);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAllBySprintIdAsync(string sprintId)
    {
        var tasks = await db.ActiveTasks.Where(t => t.SprintId == sprintId).ToListAsync();
        db.ActiveTasks.RemoveRange(tasks);
        await db.SaveChangesAsync();
    }
}
