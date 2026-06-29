using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class Ticket
{
    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public Priority Priority { get; set; } = Priority.Medium;
    public string Tier { get; set; } = "regular";
    public string? SprintId { get; set; }
    public string PlanApproach { get; set; } = string.Empty;
    public string PlanFiles { get; set; } = string.Empty;
    public DateTime? PlanApprovedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; set; }

    private Ticket() { }

    public Ticket(string id, string title, string description)
    {
        _ = new TicketId(id);
        Id = id;
        Title = title;
        Description = description;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(TicketStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPlanApproved()
    {
        PlanApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
