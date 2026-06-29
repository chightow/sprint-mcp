using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class TestPlanItemRepository(AppDbContext db) : ITestPlanItemRepository
{
    public async Task<List<TestPlanItem>> GetByTicketIdAsync(string ticketId)
    {
        return await db.TestPlanItems
            .Where(t => t.TicketId == ticketId)
            .OrderBy(t => t.Ordinal)
            .ToListAsync();
    }

    public async Task<TestPlanItem> AddAsync(TestPlanItem item)
    {
        db.TestPlanItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateAsync(TestPlanItem item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<int> GetNextOrdinalAsync(string ticketId)
    {
        var maxOrdinal = await db.TestPlanItems
            .Where(t => t.TicketId == ticketId)
            .MaxAsync(t => (int?)t.Ordinal) ?? 0;
        return maxOrdinal + 1;
    }
}
