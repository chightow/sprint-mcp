namespace SprintMcp.Domain.Entities;

public class AcceptanceCriterion
{
    public int Id { get; private set; }
    public string TicketId { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool Satisfied { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
