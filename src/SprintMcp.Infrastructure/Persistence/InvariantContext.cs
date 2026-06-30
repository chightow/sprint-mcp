using Microsoft.EntityFrameworkCore;
using SprintMcp.Application.Abstractions;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence;

public class InvariantContext(AppDbContext db) : IInvariantContext
{
    public async Task<string?> GetActiveSprintPhaseAsync(CancellationToken ct)
    {
        var sprint = await db.Sprints
            .FirstOrDefaultAsync(s => s.Status == SprintStatus.Active, ct);
        return sprint?.Phase.Value;
    }

    public async Task<string?> GetTicketStatusAsync(string ticketId, CancellationToken ct)
    {
        var ticket = await db.Tickets
            .FirstOrDefaultAsync(t => t.Id == TicketId.FromString(ticketId), ct);
        return ticket?.Status.Value;
    }
}
