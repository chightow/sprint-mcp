using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IAcceptanceCriterionRepository
{
    Task<List<AcceptanceCriterion>> GetByTicketIdAsync(string ticketId, CancellationToken ct = default);
    Task<AcceptanceCriterion?> GetByTicketIdAndIdAsync(string ticketId, int id, CancellationToken ct = default);
    Task<AcceptanceCriterion?> GetByTicketIdAndOrdinalAsync(string ticketId, int ordinal, CancellationToken ct = default);
    Task<AcceptanceCriterion> AddAsync(AcceptanceCriterion criterion, CancellationToken ct = default);
    Task UpdateAsync(AcceptanceCriterion criterion, CancellationToken ct = default);
    Task<int> GetNextOrdinalAsync(string ticketId, CancellationToken ct = default);
}
