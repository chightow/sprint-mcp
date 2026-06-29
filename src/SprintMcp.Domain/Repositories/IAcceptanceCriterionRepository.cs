using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IAcceptanceCriterionRepository
{
    Task<List<AcceptanceCriterion>> GetByTicketIdAsync(string ticketId);
    Task<AcceptanceCriterion> AddAsync(AcceptanceCriterion criterion);
    Task UpdateAsync(AcceptanceCriterion criterion);
    Task<int> GetNextOrdinalAsync(string ticketId);
}
