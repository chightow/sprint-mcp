using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(string ticketId);
    Task<List<Ticket>> GetAllAsync();
    Task<List<Ticket>> GetBySprintIdAsync(string sprintId);
    Task<Ticket> CreateAsync(string title, string description);
    Task<Ticket> CreateAsync(string title, string description, Priority priority);
    Task UpdateAsync(Ticket ticket);
    Task<string> GetNextIdAsync();
}
