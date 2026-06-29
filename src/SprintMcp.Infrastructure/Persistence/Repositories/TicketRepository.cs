using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class TicketRepository(AppDbContext db) : ITicketRepository
{
    public async Task<Ticket?> GetByIdAsync(string ticketId)
    {
        return await db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
    }

    public async Task<List<Ticket>> GetAllAsync()
    {
        return await db.Tickets.OrderBy(t => t.Id).ToListAsync();
    }

    public async Task<List<Ticket>> GetBySprintIdAsync(string sprintId)
    {
        return await db.Tickets.Where(t => t.SprintId == sprintId).OrderBy(t => t.Id).ToListAsync();
    }

    public async Task<Ticket> CreateAsync(string title, string description)
    {
        var nextId = await GetNextIdAsync();
        var ticket = new Ticket(nextId, title, description);
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    public async Task<Ticket> CreateAsync(string title, string description, Priority priority)
    {
        var nextId = await GetNextIdAsync();
        var ticket = new Ticket(nextId, title, description) { Priority = priority };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    public async Task UpdateAsync(Ticket ticket)
    {
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task<string> GetNextIdAsync()
    {
        var re = new System.Text.RegularExpressions.Regex(@"^TKT-(\d+)$");
        ulong maxN = 0;
        var ids = await db.Tickets.Select(t => t.Id).ToListAsync();
        foreach (var id in ids)
        {
            var m = re.Match(id);
            if (m.Success && ulong.TryParse(m.Groups[1].Value, out var n) && n > maxN)
                maxN = n;
        }
        return $"TKT-{maxN + 1:D4}";
    }
}
