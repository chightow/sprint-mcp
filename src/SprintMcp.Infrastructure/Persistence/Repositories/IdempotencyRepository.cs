using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class IdempotencyRepository(AppDbContext db, TimeProvider timeProvider) : IIdempotencyRepository
{
    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        return await db.IdempotencyKeys.FirstOrDefaultAsync(r => r.Key == key, ct);
    }

    public async Task StoreAsync(string key, string resultJson, CancellationToken ct = default)
    {
        var existing = await db.IdempotencyKeys.FirstOrDefaultAsync(r => r.Key == key, ct);
        if (existing is not null)
        {
            existing.ResultJson = resultJson;
        }
        else
        {
            db.IdempotencyKeys.Add(new IdempotencyRecord(key, resultJson));
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var record = await db.IdempotencyKeys.FirstOrDefaultAsync(r => r.Key == key, ct);
        if (record is not null)
        {
            db.IdempotencyKeys.Remove(record);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task PurgeExpiredAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - maxAge;
        var expired = await db.IdempotencyKeys
            .Where(r => r.CreatedAt < cutoff)
            .ToListAsync(ct);
        if (expired.Count > 0)
        {
            db.IdempotencyKeys.RemoveRange(expired);
            await db.SaveChangesAsync(ct);
        }
    }
}
