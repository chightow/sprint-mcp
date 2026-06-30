using System.Text.Json;

namespace SprintMcp.Domain.Entities;

public class Event
{
    public long Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string AggregateType { get; private set; } = string.Empty;
    public string AggregateId { get; private set; } = string.Empty;
    public string? ProposedBy { get; private set; }
    public string CausedBy { get; private set; } = "[]";
    public DateTime OccurredAt { get; private set; }
    public string EventData { get; private set; } = "{}";
    public string? Signature { get; private set; }

    private Event() { }

    public Event(
        string eventType,
        string category,
        string aggregateType,
        string aggregateId,
        string? proposedBy,
        string[] causedBy,
        DateTime occurredAt,
        string eventDataJson,
        string? signature = null)
    {
        EventType = eventType;
        Category = category;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        ProposedBy = proposedBy;
        CausedBy = JsonSerializer.Serialize(causedBy);
        OccurredAt = occurredAt;
        EventData = eventDataJson;
        Signature = signature;
    }

    public string[] GetCausedBy() =>
        JsonSerializer.Deserialize<string[]>(CausedBy) ?? [];
}
