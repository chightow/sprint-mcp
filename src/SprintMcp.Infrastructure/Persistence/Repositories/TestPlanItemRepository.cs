using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class TestPlanItemRepository(AppDbContext db) : ITestPlanItemRepository
{
    public async Task<List<TestPlanItem>> GetByTicketIdAsync(string ticketId, CancellationToken ct = default)
    {
        return await db.TestPlanItems
            .Where(t => t.TicketId == ticketId)
            .OrderBy(t => t.Ordinal)
            .ToListAsync(ct);
    }

    public async Task<TestPlanItem> AddAsync(TestPlanItem item, CancellationToken ct = default)
    {
        db.TestPlanItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task UpdateAsync(TestPlanItem item, CancellationToken ct = default)
    {
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetNextOrdinalAsync(string ticketId, CancellationToken ct = default)
    {
        var maxOrdinal = await db.TestPlanItems
            .Where(t => t.TicketId == ticketId)
            .MaxAsync(t => (int?)t.Ordinal, ct) ?? 0;
        return maxOrdinal + 1;
    }
}
