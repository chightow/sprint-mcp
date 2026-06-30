using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class ActiveTask
{
    public int Id { get; private set; }
    public SprintId SprintId { get; private set; } = null!;
    public string TaskRef { get; private set; } = string.Empty;
    public int Ordinal { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private ActiveTask() { }

    public ActiveTask(SprintId sprintId, string taskRef, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskRef, nameof(taskRef));
        SprintId = sprintId;
        TaskRef = taskRef;
        Ordinal = ordinal;
    }
}
