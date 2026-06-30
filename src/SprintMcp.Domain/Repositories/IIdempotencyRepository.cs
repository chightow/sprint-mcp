using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default);
    Task StoreAsync(string key, string resultJson, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task PurgeExpiredAsync(TimeSpan maxAge, CancellationToken ct = default);
}
