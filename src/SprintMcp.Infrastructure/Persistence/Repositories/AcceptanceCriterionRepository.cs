using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class AcceptanceCriterionRepository(AppDbContext db) : IAcceptanceCriterionRepository
{
    public async Task<List<AcceptanceCriterion>> GetByTicketIdAsync(string ticketId)
    {
        return await db.AcceptanceCriteria
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.Ordinal)
            .ToListAsync();
    }

    public async Task<AcceptanceCriterion> AddAsync(AcceptanceCriterion criterion)
    {
        db.AcceptanceCriteria.Add(criterion);
        await db.SaveChangesAsync();
        return criterion;
    }

    public async Task UpdateAsync(AcceptanceCriterion criterion)
    {
        await db.SaveChangesAsync();
    }

    public async Task<int> GetNextOrdinalAsync(string ticketId)
    {
        var maxOrdinal = await db.AcceptanceCriteria
            .Where(c => c.TicketId == ticketId)
            .MaxAsync(c => (int?)c.Ordinal) ?? 0;
        return maxOrdinal + 1;
    }
}
