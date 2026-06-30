using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class TestPlanItem
{
    public int Id { get; private set; }
    public string TicketId { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public TestPlanStatus Status { get; set; } = TestPlanStatus.Pending;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
