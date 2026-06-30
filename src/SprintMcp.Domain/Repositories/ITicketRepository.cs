using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(TicketId ticketId, CancellationToken ct = default);
    Task<List<Ticket>> GetAllAsync(int skip = 0, int take = 100, CancellationToken ct = default);
    Task<List<Ticket>> GetBySprintIdAsync(SprintId sprintId, CancellationToken ct = default);
    Task<Ticket> CreateAsync(string title, string description, Priority priority, CancellationToken ct = default);
    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
    Task<string> GetNextIdAsync(CancellationToken ct = default);
}
