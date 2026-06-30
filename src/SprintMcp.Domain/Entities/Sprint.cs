using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class Sprint
{
    public SprintId Id { get; private set; } = null!;
    public SprintStatus Status { get; private set; } = SprintStatus.Active;
    public SprintPhase Phase { get; private set; } = SprintPhase.Planning;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; private set; }

    private Sprint() { }

    public static Sprint Create(SprintId id)
    {
        return new Sprint
        {
            Id = id,
            StartedAt = DateTime.UtcNow
        };
    }

    public void AdvancePhase()
    {
        Phase = Phase.Next();
    }

    public void Close(DateTime closedAt)
    {
        if (!Status.CanTransitionTo(SprintStatus.Closed))
            throw new InvalidOperationException($"Cannot close sprint from status '{Status}'.");
        if (!Phase.CanTransitionTo(SprintPhase.Complete))
            throw new InvalidOperationException($"Cannot close sprint in phase '{Phase}'. Must be in evaluating phase.");
        Phase = SprintPhase.Complete;
        Status = SprintStatus.Closed;
        ClosedAt = closedAt;
    }
}
