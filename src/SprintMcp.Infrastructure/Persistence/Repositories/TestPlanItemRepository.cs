using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class TestPlanItemRepository(AppDbContext db) : ITestPlanItemRepository
{
    public async Task<List<TestPlanItem>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default)
    {
        return await db.TestPlanItems
            .Where(t => t.TicketId == ticketId)
            .OrderBy(t => t.Ordinal)
            .ToListAsync(ct);
    }

    public async Task<TestPlanItem?> GetByTicketIdAndOrdinalAsync(TicketId ticketId, int ordinal, CancellationToken ct = default)
    {
        return await db.TestPlanItems
            .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.Ordinal == ordinal, ct);
    }

    public async Task<TestPlanItem> AddAsync(TestPlanItem item, CancellationToken ct = default)
    {
        db.TestPlanItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task UpdateAsync(TestPlanItem item, CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetNextOrdinalAsync(TicketId ticketId, CancellationToken ct = default)
    {
        var maxOrdinal = await db.TestPlanItems
            .Where(t => t.TicketId == ticketId)
            .MaxAsync(t => (int?)t.Ordinal, ct) ?? 0;
        return maxOrdinal + 1;
    }
}
