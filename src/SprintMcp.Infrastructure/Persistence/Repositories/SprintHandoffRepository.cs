using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class SprintHandoffRepository(AppDbContext db) : ISprintHandoffRepository
{
    public async Task<SprintHandoff?> GetBySprintIdAsync(SprintId sprintId, CancellationToken ct = default)
    {
        return await db.SprintHandoffs.FirstOrDefaultAsync(h => h.SprintId == sprintId, ct);
    }

    public async Task UpsertAsync(SprintHandoff handoff, CancellationToken ct = default)
    {
        var existing = await db.SprintHandoffs.FirstOrDefaultAsync(h => h.SprintId == handoff.SprintId, ct);
        if (existing is not null)
        {
            existing.UpdateFocus(handoff.CurrentFocus, handoff.UpdatedAt);
            existing.UpdateInProgress(handoff.InProgress, handoff.UpdatedAt);
            existing.UpdateDiscoveries(handoff.Discoveries, handoff.UpdatedAt);
            existing.UpdateNextSteps(handoff.NextSteps, handoff.UpdatedAt);
        }
        else
        {
            db.SprintHandoffs.Add(handoff);
        }
        await db.SaveChangesAsync(ct);
    }
}
