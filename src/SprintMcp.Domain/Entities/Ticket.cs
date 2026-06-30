using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class Ticket
{
    public string Id { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public Priority Priority { get; private set; } = Priority.Medium;
    public TicketTier Tier { get; private set; } = TicketTier.Regular;
    public string? SprintId { get; private set; }
    public string PlanApproach { get; private set; } = string.Empty;
    public string PlanFiles { get; private set; } = string.Empty;
    public DateTime? PlanApprovedAt { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Ticket() { }

    public Ticket(string id, string title, string description)
    {
        TicketId.Validate(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        Id = id;
        Title = title;
        Description = description ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeStatus(TicketStatus newStatus)
    {
        if (!Status.CanTransitionTo(newStatus))
            throw new InvalidOperationException($"Cannot transition from '{Status}' to '{newStatus}'.");
        Status = newStatus;
    }

    public void ChangePriority(Priority newPriority)
    {
        Priority = newPriority;
    }

    public void ChangeTier(TicketTier newTier)
    {
        Tier = newTier;
    }

    public void MarkPlanApproved(DateTime timestamp)
    {
        PlanApprovedAt = timestamp;
    }

    public void SetPlanApproach(string approach)
    {
        PlanApproach = approach;
    }

    public void SetPlanFiles(string files)
    {
        PlanFiles = files;
    }

    public void SetSummary(string summary)
    {
        Summary = summary;
    }

    public void AssignToSprint(string sprintId)
    {
        ValueObjects.SprintId.Validate(sprintId);
        SprintId = sprintId;
    }

    public void Touch(DateTime by)
    {
        UpdatedAt = by;
    }
}
