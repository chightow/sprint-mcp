using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class EvalReportRepository(AppDbContext db) : IEvalReportRepository
{
    public async Task<EvalReport?> GetByTicketIdAsync(string ticketId)
    {
        return await db.EvalReports.FirstOrDefaultAsync(e => e.TicketId == ticketId);
    }

    public async Task UpsertAsync(EvalReport report)
    {
        var existing = await db.EvalReports.FirstOrDefaultAsync(e => e.TicketId == report.TicketId);
        if (existing is not null)
        {
            existing.RunId = report.RunId;
            existing.Verdict = report.Verdict;
            existing.Content = report.Content;
            existing.MatchedRunTs = report.MatchedRunTs;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            report.UpdatedAt = DateTime.UtcNow;
            db.EvalReports.Add(report);
        }
        await db.SaveChangesAsync();
    }
}
