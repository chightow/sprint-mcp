using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IDecisionRepository
{
    Task<List<Decision>> GetByTicketIdAsync(string ticketId, CancellationToken ct = default);
    Task AddAsync(Decision decision, CancellationToken ct = default);
}
