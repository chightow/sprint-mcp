using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface ITestPlanItemRepository
{
    Task<List<TestPlanItem>> GetByTicketIdAsync(string ticketId);
    Task<TestPlanItem> AddAsync(TestPlanItem item);
    Task UpdateAsync(TestPlanItem item);
    Task<int> GetNextOrdinalAsync(string ticketId);
}
