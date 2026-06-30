using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface IEvalReportRepository
{
    Task<EvalReport?> GetByTicketIdAsync(TicketId ticketId, CancellationToken ct = default);
    Task UpsertAsync(EvalReport report, CancellationToken ct = default);
}
