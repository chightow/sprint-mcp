using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface ITestPlanItemRepository
{
    Task<List<TestPlanItem>> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default);
    Task<TestPlanItem?> GetByTicketIdAndOrdinalAsync(TicketId ticketId, int ordinal, CancellationToken ct = default);
    Task<TestPlanItem> AddAsync(TestPlanItem item, CancellationToken ct = default);
    Task UpdateAsync(TestPlanItem item, CancellationToken ct = default);
    Task<int> GetNextOrdinalAsync(TicketId ticketId, CancellationToken ct = default);
}
