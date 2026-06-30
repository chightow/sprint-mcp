using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class AcceptanceCriterion
{
    public int Id { get; private set; }
    public TicketId TicketId { get; private set; } = null!;
    public int Ordinal { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool Satisfied { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AcceptanceCriterion() { }

    public AcceptanceCriterion(TicketId ticketId, int ordinal, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        TicketId = ticketId;
        Ordinal = ordinal;
        Text = text;
    }
}
