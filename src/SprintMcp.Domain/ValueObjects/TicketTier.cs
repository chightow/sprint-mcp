namespace SprintMcp.Domain.ValueObjects;

public record TicketTier
{
    public static readonly TicketTier Trivial = new("trivial");
    public static readonly TicketTier Regular = new("regular");
    public static readonly TicketTier Complex = new("complex");

    private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "trivial", "regular", "complex"
    };

    public string Value { get; }

    private TicketTier(string value) => Value = value;

    public static TicketTier FromString(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid tier '{value}'. Must be one of: trivial, regular, complex", nameof(value));
        return new TicketTier(value.ToLowerInvariant());
    }

    public static TicketTier Default => Regular;
    public override string ToString() => Value;
}
