namespace SprintMcp.Domain.Entities;

public class Decision
{
    public int Id { get; private set; }
    public string TicketId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Rationale { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private Decision() { }

    public Decision(string ticketId, string title, string rationale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId, nameof(ticketId));
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        TicketId = ticketId;
        Title = title;
        Rationale = rationale ?? string.Empty;
    }
}
