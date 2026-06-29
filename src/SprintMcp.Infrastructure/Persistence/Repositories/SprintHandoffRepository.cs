using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class SprintHandoffRepository(AppDbContext db) : ISprintHandoffRepository
{
    public async Task<SprintHandoff?> GetBySprintIdAsync(string sprintId)
    {
        return await db.SprintHandoffs.FirstOrDefaultAsync(h => h.SprintId == sprintId);
    }

    public async Task UpdateAsync(SprintHandoff handoff)
    {
        handoff.UpdatedAt = DateTime.UtcNow;
        var existing = await db.SprintHandoffs.FirstOrDefaultAsync(h => h.SprintId == handoff.SprintId);
        if (existing is not null)
        {
            existing.CurrentFocus = handoff.CurrentFocus;
            existing.InProgress = handoff.InProgress;
            existing.Discoveries = handoff.Discoveries;
            existing.NextSteps = handoff.NextSteps;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.SprintHandoffs.Add(handoff);
        }
        await db.SaveChangesAsync();
    }
}
