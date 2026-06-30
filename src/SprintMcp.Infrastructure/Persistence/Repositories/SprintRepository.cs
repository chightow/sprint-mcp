using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class SprintRepository(AppDbContext db) : ISprintRepository
{
    private static readonly SemaphoreSlim _idLock = new(1, 1);

    public async Task<Sprint?> GetActiveAsync(CancellationToken ct = default)
    {
        return await db.Sprints.FirstOrDefaultAsync(s => s.Status == SprintStatus.Active, ct);
    }

    public async Task<List<Sprint>> GetAllActiveAsync(CancellationToken ct = default)
    {
        return await db.Sprints.Where(s => s.Status == SprintStatus.Active).ToListAsync(ct);
    }

    public async Task<Sprint?> GetByIdAsync(SprintId sprintId, CancellationToken ct = default)
    {
        return await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId, ct);
    }

    public async Task<Sprint> CreateAsync(SprintId id, CancellationToken ct = default)
    {
        var sprint = Sprint.Create(id);
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);
        return sprint;
    }

    public async Task<Sprint> CreateNextAsync(CancellationToken ct = default)
    {
        await _idLock.WaitAsync(ct);
        try
        {
            var idStr = await GetNextIdInternalAsync(ct);
            var sprint = Sprint.Create(SprintId.FromString(idStr));
            db.Sprints.Add(sprint);
            await db.SaveChangesAsync(ct);
            return sprint;
        }
        finally
        {
            _idLock.Release();
        }
    }

    public async Task UpdateAsync(Sprint sprint, CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }

    private async Task<string> GetNextIdInternalAsync(CancellationToken ct = default)
    {
        var maxN = await db.Database
            .SqlQueryRaw<long>(
                "SELECT COALESCE(MAX(CAST(SUBSTR(Id, 8) AS INTEGER)), 0) AS Value FROM Sprints")
            .FirstOrDefaultAsync(ct);
        return $"SPRINT-{maxN + 1:D4}";
    }
}
