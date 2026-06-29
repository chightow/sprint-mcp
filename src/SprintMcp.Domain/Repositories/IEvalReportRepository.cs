using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IEvalReportRepository
{
    Task<EvalReport?> GetByTicketIdAsync(string ticketId);
    Task UpsertAsync(EvalReport report);
}
