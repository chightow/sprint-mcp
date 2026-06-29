namespace SprintMcp.Domain.Entities;

public class ActiveTask
{
    public int Id { get; private set; }
    public string SprintId { get; set; } = string.Empty;
    public string TaskRef { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
