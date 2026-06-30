using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class Decision
{
    public int Id { get; private set; }
    public TicketId TicketId { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string Rationale { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Decision() { }

    public Decision(TicketId ticketId, string title, string rationale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        TicketId = ticketId;
        Title = title;
        Rationale = rationale ?? string.Empty;
    }
}
