namespace SprintMcp.Domain.Entities;

public class Sprint
{
    public string Id { get; private set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    private Sprint() { }

    public Sprint(string id)
    {
        Id = id;
        StartedAt = DateTime.UtcNow;
    }
}
