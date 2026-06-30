namespace SprintMcp.Domain.ValueObjects;

public record SprintStatus
{
    public static readonly SprintStatus Active = new("active");
    public static readonly SprintStatus Closed = new("closed");

    private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "active", "closed"
    };

    private static readonly Dictionary<SprintStatus, HashSet<SprintStatus>> AllowedTransitions = new()
    {
        [Active] = new() { Closed },
        [Closed] = new() { },
    };

    public string Value { get; }

    private SprintStatus(string value) => Value = value;

    public static SprintStatus FromString(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid sprint status '{value}'. Must be one of: active, closed", nameof(value));
        return new SprintStatus(value.ToLowerInvariant());
    }

    public bool CanTransitionTo(SprintStatus target) =>
        AllowedTransitions.TryGetValue(this, out var allowed) && allowed.Contains(target);

    public bool IsTerminal => Value == "closed";
    public override string ToString() => Value;
}
