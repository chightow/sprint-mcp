using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class Sprint
{
    public string Id { get; private set; } = string.Empty;
    public SprintStatus Status { get; private set; } = SprintStatus.Active;
    public SprintPhase Phase { get; private set; } = SprintPhase.Planning;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; private set; }

    private Sprint() { }

    public Sprint(string id)
    {
        _ = new SprintId(id);
        Id = id;
        StartedAt = DateTime.UtcNow;
    }

    public void AdvancePhase()
    {
        Phase = Phase.Next();
    }

    public void Close()
    {
        if (!Phase.CanTransitionTo(SprintPhase.Complete))
            throw new InvalidOperationException($"Cannot close sprint in phase '{Phase}'. Must be in evaluating phase.");
        Phase = SprintPhase.Complete;
        Status = SprintStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }
}
