using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class EvalReportRepository(AppDbContext db) : IEvalReportRepository
{
    public async Task<EvalReport?> GetByTicketIdAsync(string ticketId, CancellationToken ct = default)
    {
        return await db.EvalReports.FirstOrDefaultAsync(e => e.TicketId == ticketId, ct);
    }

    public async Task UpsertAsync(EvalReport report, CancellationToken ct = default)
    {
        var existing = await db.EvalReports.FirstOrDefaultAsync(e => e.TicketId == report.TicketId, ct);
        if (existing is not null)
        {
            existing.Update(report.RunId, report.Verdict, report.Content, report.UpdatedAt, report.MatchedRunTs);
        }
        else
        {
            db.EvalReports.Add(report);
        }
        await db.SaveChangesAsync(ct);
    }
}
