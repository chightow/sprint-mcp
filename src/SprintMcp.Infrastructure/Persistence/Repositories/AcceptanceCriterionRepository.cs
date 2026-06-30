using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class AcceptanceCriterionRepository(AppDbContext db) : IAcceptanceCriterionRepository
{
    public async Task<List<AcceptanceCriterion>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default)
    {
        return await db.AcceptanceCriteria
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.Ordinal)
            .ToListAsync(ct);
    }

    public async Task<AcceptanceCriterion?> GetByTicketIdAndIdAsync(TicketId ticketId, int id, CancellationToken ct = default)
    {
        return await db.AcceptanceCriteria
            .FirstOrDefaultAsync(c => c.TicketId == ticketId && c.Id == id, ct);
    }

    public async Task<AcceptanceCriterion?> GetByTicketIdAndOrdinalAsync(TicketId ticketId, int ordinal, CancellationToken ct = default)
    {
        return await db.AcceptanceCriteria
            .FirstOrDefaultAsync(c => c.TicketId == ticketId && c.Ordinal == ordinal, ct);
    }

    public async Task<AcceptanceCriterion> AddAsync(AcceptanceCriterion criterion, CancellationToken ct = default)
    {
        db.AcceptanceCriteria.Add(criterion);
        await db.SaveChangesAsync(ct);
        return criterion;
    }

    public async Task UpdateAsync(AcceptanceCriterion criterion, CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetNextOrdinalAsync(TicketId ticketId, CancellationToken ct = default)
    {
        var maxOrdinal = await db.AcceptanceCriteria
            .Where(c => c.TicketId == ticketId)
            .MaxAsync(c => (int?)c.Ordinal, ct) ?? 0;
        return maxOrdinal + 1;
    }
}
