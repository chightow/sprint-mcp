using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface IEvalReportRepository
{
    Task<EvalReport?> GetByTicketIdAsync(string ticketId, CancellationToken ct = default);
    Task UpsertAsync(EvalReport report, CancellationToken ct = default);
}
