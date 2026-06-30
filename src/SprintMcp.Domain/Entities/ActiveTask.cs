namespace SprintMcp.Domain.Entities;

public class ActiveTask
{
    public int Id { get; private set; }
    public string SprintId { get; private set; } = string.Empty;
    public string TaskRef { get; private set; } = string.Empty;
    public int Ordinal { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private ActiveTask() { }

    public ActiveTask(string sprintId, string taskRef, int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sprintId, nameof(sprintId));
        ArgumentException.ThrowIfNullOrWhiteSpace(taskRef, nameof(taskRef));
        SprintId = sprintId;
        TaskRef = taskRef;
        Ordinal = ordinal;
    }
}
