namespace SprintMcp.Domain.Entities;

public class AcceptanceCriterion
{
    public int Id { get; private set; }
    public string TicketId { get; private set; } = string.Empty;
    public int Ordinal { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public bool Satisfied { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private AcceptanceCriterion() { }

    public AcceptanceCriterion(string ticketId, int ordinal, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId, nameof(ticketId));
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        TicketId = ticketId;
        Ordinal = ordinal;
        Text = text;
    }
}
