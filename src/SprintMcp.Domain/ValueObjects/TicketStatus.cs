namespace SprintMcp.Domain.ValueObjects;

public record TicketStatus
{
    public static readonly TicketStatus Open = new("open");
    public static readonly TicketStatus InProgress = new("in_progress");
    public static readonly TicketStatus Closed = new("closed");
    public static readonly TicketStatus Cancelled = new("cancelled");
    public static readonly TicketStatus Archived = new("archived");

    private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "in_progress", "closed", "cancelled", "archived"
    };

    public string Value { get; }

    private TicketStatus(string value) => Value = value;

    public static TicketStatus FromString(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid status '{value}'. Must be one of: open, in_progress, closed, cancelled, archived", nameof(value));
        return new TicketStatus(value.ToLowerInvariant());
    }

    public bool IsTerminal => Value is "closed" or "cancelled" or "archived";

    public override string ToString() => Value;
}
