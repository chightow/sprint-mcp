using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface IDecisionRepository
{
    Task<List<Decision>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default);
    Task AddAsync(Decision decision, CancellationToken ct = default);
}
