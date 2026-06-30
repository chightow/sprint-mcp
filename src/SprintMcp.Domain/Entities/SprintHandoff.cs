using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class SprintHandoff
{
    public string SprintId { get; private set; } = string.Empty;
    public string CurrentFocus { get; set; } = string.Empty;
    public string InProgress { get; set; } = string.Empty;
    public string Discoveries { get; set; } = string.Empty;
    public string NextSteps { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    private SprintHandoff() { }

    public SprintHandoff(string sprintId)
    {
        _ = new SprintId(sprintId);
        SprintId = sprintId;
    }
}
