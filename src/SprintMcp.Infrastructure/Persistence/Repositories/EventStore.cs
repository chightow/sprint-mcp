using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class EventStore(AppDbContext db) : IEventStore
{
    public void Track(Event evt) => db.Events.Add(evt);

    public async Task<Event> AppendAsync(Event evt, CancellationToken ct = default)
    {
        db.Events.Add(evt);
        await db.SaveChangesAsync(ct);
        return evt;
    }

    public async Task<List<Event>> GetSinceAsync(long since, int take = 100, string? typeFilter = null, string? aggregateIdFilter = null, CancellationToken ct = default)
    {
        var query = db.Events.Where(e => e.Id > since);

        if (typeFilter is not null)
            query = query.Where(e => e.EventType == typeFilter);

        if (aggregateIdFilter is not null)
            query = query.Where(e => e.AggregateId == aggregateIdFilter);

        return await query.OrderBy(e => e.Id).Take(take).ToListAsync(ct);
    }

    public async Task<long> GetMaxIdAsync(CancellationToken ct = default)
    {
        return await db.Events.MaxAsync(e => (long?)e.Id, ct) ?? 0;
    }
}
