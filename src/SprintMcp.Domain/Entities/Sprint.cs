using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class Sprint
{
    public string Id { get; private set; } = string.Empty;
    public SprintStatus Status { get; private set; } = SprintStatus.Active;
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    private Sprint() { }

    public Sprint(string id)
    {
        _ = new SprintId(id);
        Id = id;
        StartedAt = DateTime.UtcNow;
    }

    public void Close()
    {
        Status = SprintStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }
}
