using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface ITestPlanItemRepository
{
    Task<List<TestPlanItem>> GetByTicketIdAsync(string ticketId, CancellationToken ct = default);
    Task<TestPlanItem> AddAsync(TestPlanItem item, CancellationToken ct = default);
    Task UpdateAsync(TestPlanItem item, CancellationToken ct = default);
    Task<int> GetNextOrdinalAsync(string ticketId, CancellationToken ct = default);
}
