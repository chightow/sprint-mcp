using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class Ticket
{
    public TicketId Id { get; private set; } = null!;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public Priority Priority { get; private set; } = Priority.Medium;
    public TicketTier Tier { get; private set; } = TicketTier.Regular;
    public SprintId? SprintId { get; private set; }
    public string PlanApproach { get; private set; } = string.Empty;
    public string PlanFiles { get; private set; } = string.Empty;
    public DateTime? PlanApprovedAt { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Ticket() { }

    public static Ticket Create(TicketId id, string title, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        return new Ticket
        {
            Id = id,
            Title = title,
            Description = description ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void TransitionTo(TicketStatus newStatus)
    {
        if (!Status.CanTransitionTo(newStatus))
            throw new InvalidOperationException($"Cannot transition from '{Status}' to '{newStatus}'.");
        Status = newStatus;
    }

    public void PrioritizeAs(Priority newPriority)
    {
        Priority = newPriority;
    }

    public void CategorizeAs(TicketTier newTier)
    {
        Tier = newTier;
    }

    public void ApprovePlan(DateTime timestamp)
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

    public void AssignToSprint(SprintId sprintId)
    {
        SprintId = sprintId;
    }

    public void Touch(DateTime by)
    {
        UpdatedAt = by;
    }
}
