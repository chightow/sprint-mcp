using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class SprintRepository(AppDbContext db) : ISprintRepository
{
    public async Task<Sprint?> GetActiveAsync(CancellationToken ct = default)
    {
        return await db.Sprints.FirstOrDefaultAsync(s => s.Status == SprintStatus.Active, ct);
    }

    public async Task<Sprint?> GetByIdAsync(string sprintId, CancellationToken ct = default)
    {
        return await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId, ct);
    }

    public async Task<Sprint> CreateAsync(string id, CancellationToken ct = default)
    {
        var sprint = new Sprint(id);
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);
        return sprint;
    }

    public async Task UpdateAsync(Sprint sprint, CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> GetNextIdAsync(CancellationToken ct = default)
    {
        var maxN = await db.Database
            .SqlQueryRaw<long>(
                "SELECT COALESCE(MAX(CAST(SUBSTR(Id, 8) AS INTEGER)), 0) AS Value FROM Sprints")
            .FirstOrDefaultAsync(ct);
        return $"SPRINT-{maxN + 1:D4}";
    }
}
