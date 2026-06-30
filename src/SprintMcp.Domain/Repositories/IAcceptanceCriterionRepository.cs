using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface IAcceptanceCriterionRepository
{
    Task<List<AcceptanceCriterion>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default);
    Task<AcceptanceCriterion?> GetByTicketIdAndIdAsync(TicketId ticketId, int id, CancellationToken ct = default);
    Task<AcceptanceCriterion?> GetByTicketIdAndOrdinalAsync(TicketId ticketId, int ordinal, CancellationToken ct = default);
    Task<AcceptanceCriterion> AddAsync(AcceptanceCriterion criterion, CancellationToken ct = default);
    Task UpdateAsync(AcceptanceCriterion criterion, CancellationToken ct = default);
    Task<int> GetNextOrdinalAsync(TicketId ticketId, CancellationToken ct = default);
}
