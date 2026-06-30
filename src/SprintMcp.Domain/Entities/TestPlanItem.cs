using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class TestPlanItem
{
    public int Id { get; private set; }
    public TicketId TicketId { get; private set; } = null!;
    public int Ordinal { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Expected { get; private set; } = string.Empty;
    public TestPlanStatus Status { get; set; } = TestPlanStatus.Pending;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    private TestPlanItem() { }

    public TestPlanItem(TicketId ticketId, int ordinal, string description, string expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));
        TicketId = ticketId;
        Ordinal = ordinal;
        Description = description;
        Expected = expected ?? string.Empty;
    }
}
