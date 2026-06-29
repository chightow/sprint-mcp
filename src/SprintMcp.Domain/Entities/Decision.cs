namespace SprintMcp.Domain.Entities;

public class Decision
{
    public int Id { get; private set; }
    public string TicketId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
