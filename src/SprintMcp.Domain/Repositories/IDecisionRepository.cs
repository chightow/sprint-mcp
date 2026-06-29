using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IDecisionRepository
{
    Task<List<Decision>> GetByTicketIdAsync(string ticketId);
    Task AddAsync(Decision decision);
}
