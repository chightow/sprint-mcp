using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class SprintHandoff
{
    public SprintId SprintId { get; private set; } = null!;
    public string CurrentFocus { get; private set; } = string.Empty;
    public string InProgress { get; private set; } = string.Empty;
    public string Discoveries { get; private set; } = string.Empty;
    public string NextSteps { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private SprintHandoff() { }

    public SprintHandoff(SprintId sprintId)
    {
        SprintId = sprintId;
    }

    public void UpdateFocus(string currentFocus, DateTime updatedAt)
    {
        CurrentFocus = currentFocus;
        UpdatedAt = updatedAt;
    }

    public void UpdateInProgress(string inProgress, DateTime updatedAt)
    {
        InProgress = inProgress;
        UpdatedAt = updatedAt;
    }

    public void UpdateDiscoveries(string discoveries, DateTime updatedAt)
    {
        Discoveries = discoveries;
        UpdatedAt = updatedAt;
    }

    public void UpdateNextSteps(string nextSteps, DateTime updatedAt)
    {
        NextSteps = nextSteps;
        UpdatedAt = updatedAt;
    }
}
