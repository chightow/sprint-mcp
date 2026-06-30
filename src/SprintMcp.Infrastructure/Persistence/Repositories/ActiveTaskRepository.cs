using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class ActiveTaskRepository(AppDbContext db) : IActiveTaskRepository
{
    public async Task<List<ActiveTask>> GetBySprintIdAsync(string sprintId, CancellationToken ct = default)
    {
        return await db.ActiveTasks
            .Where(t => t.SprintId == sprintId)
            .OrderBy(t => t.Ordinal)
            .ToListAsync(ct);
    }

    public async Task<ActiveTask?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.ActiveTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task AddAsync(ActiveTask task, CancellationToken ct = default)
    {
        db.ActiveTasks.Add(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteByIdAsync(int id, CancellationToken ct = default)
    {
        var task = await db.ActiveTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return false;
        db.ActiveTasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DeleteAllBySprintIdAsync(string sprintId, CancellationToken ct = default)
    {
        var tasks = await db.ActiveTasks.Where(t => t.SprintId == sprintId).ToListAsync(ct);
        db.ActiveTasks.RemoveRange(tasks);
        await db.SaveChangesAsync(ct);
    }
}
