using SprintMcp.Domain.Entities;

namespace SprintMcp.Domain.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(string ticketId);
    Task<List<Ticket>> GetAllAsync();
    Task<List<Ticket>> GetBySprintIdAsync(string sprintId);
    Task<Ticket> CreateAsync(string title, string description);
    Task UpdateAsync(Ticket ticket);
    Task<string> GetNextIdAsync();
}
