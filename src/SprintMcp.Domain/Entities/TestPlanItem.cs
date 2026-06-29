namespace SprintMcp.Domain.Entities;

public class TestPlanItem
{
    public int Id { get; private set; }
    public string TicketId { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
