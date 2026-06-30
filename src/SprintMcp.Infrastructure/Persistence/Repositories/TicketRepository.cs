using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class TicketRepository(AppDbContext db) : ITicketRepository
{
    private static readonly SemaphoreSlim _idLock = new(1, 1);
    public async Task<Ticket?> GetByIdAsync(string ticketId, CancellationToken ct = default)
    {
        return await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
    }

    public async Task<List<Ticket>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Tickets.OrderBy(t => t.Id).ToListAsync(ct);
    }

    public async Task<List<Ticket>> GetBySprintIdAsync(string sprintId, CancellationToken ct = default)
    {
        return await db.Tickets.Where(t => t.SprintId == sprintId).OrderBy(t => t.Id).ToListAsync(ct);
    }

    public async Task<Ticket> CreateAsync(string title, string description, Priority priority, CancellationToken ct = default)
    {
        await _idLock.WaitAsync(ct);
        try
        {
            var nextId = await GetNextIdAsync(ct);
            var ticket = new Ticket(nextId, title, description);
            ticket.ChangePriority(priority);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync(ct);
            return ticket;
        }
        finally
        {
            _idLock.Release();
        }
    }

    public async Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }

    public async Task<string> GetNextIdAsync(CancellationToken ct = default)
    {
        var maxN = await db.Database
            .SqlQueryRaw<long>(
                "SELECT COALESCE(MAX(CAST(SUBSTR(Id, 5) AS INTEGER)), 0) AS Value FROM Tickets")
            .FirstOrDefaultAsync(ct);
        return $"TKT-{maxN + 1:D4}";
    }
}
