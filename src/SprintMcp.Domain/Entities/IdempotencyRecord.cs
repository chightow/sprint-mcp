namespace SprintMcp.Domain.Entities;

public class IdempotencyRecord
{
    public string Key { get; private set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    private IdempotencyRecord() { }

    public IdempotencyRecord(string key, string resultJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(key));
        Key = key;
        ResultJson = resultJson ?? string.Empty;
    }
}
