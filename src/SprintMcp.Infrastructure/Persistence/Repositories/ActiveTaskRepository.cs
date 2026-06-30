using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class ActiveTaskRepository(AppDbContext db) : IActiveTaskRepository
{
    public async Task<List<ActiveTask>> GetBySprintIdAsync(SprintId sprintId, CancellationToken ct = default)
    {
        return await db.ActiveTasks
            .Where(t => t.SprintId == sprintId)
            .OrderBy(t => t.Ordinal)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ActiveTask task, CancellationToken ct = default)
    {
        db.ActiveTasks.Add(task);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteBySprintIdAndIdAsync(SprintId sprintId, int id, CancellationToken ct = default)
    {
        var deleted = await db.ActiveTasks
            .Where(t => t.SprintId == sprintId && t.Id == id)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }
}
