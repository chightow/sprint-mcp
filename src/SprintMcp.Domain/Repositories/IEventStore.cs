using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IEventStore
{
    void Track(Event evt);
    Task<Event> AppendAsync(Event evt, CancellationToken ct = default);
    Task<List<Event>> GetSinceAsync(long since, int take = 100, string? typeFilter = null, string? aggregateIdFilter = null, CancellationToken ct = default);
    Task<long> GetMaxIdAsync(CancellationToken ct = default);
}
