namespace SprintMcp.Domain.ValueObjects;

public record Priority
{
    public static readonly Priority Low = new("low");
    public static readonly Priority Medium = new("medium");
    public static readonly Priority High = new("high");
    public static readonly Priority Critical = new("critical");

    private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "low", "medium", "high", "critical"
    };

    public string Value { get; }

    private Priority(string value) => Value = value;

    public static Priority FromString(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid priority '{value}'. Must be one of: low, medium, high, critical", nameof(value));
        return new Priority(value.ToLowerInvariant());
    }

    public static Priority Default => Medium;
    public override string ToString() => Value;
}
