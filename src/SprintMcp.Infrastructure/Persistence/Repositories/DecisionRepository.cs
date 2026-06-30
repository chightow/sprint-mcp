using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class DecisionRepository(AppDbContext db) : IDecisionRepository
{
    public async Task<List<Decision>> GetByTicketIdAsync(string ticketId, CancellationToken ct = default)
    {
        return await db.Decisions.Where(d => d.TicketId == ticketId).OrderBy(d => d.Id).ToListAsync(ct);
    }

    public async Task AddAsync(Decision decision, CancellationToken ct = default)
    {
        db.Decisions.Add(decision);
        await db.SaveChangesAsync(ct);
    }
}
