namespace SprintMcp.Domain.ValueObjects;

public record SprintPhase
{
    public static readonly SprintPhase Planning = new("planning");
    public static readonly SprintPhase Executing = new("executing");
    public static readonly SprintPhase Evaluating = new("evaluating");
    public static readonly SprintPhase Complete = new("complete");
    public static readonly SprintPhase Failed = new("failed");

    private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "planning", "executing", "evaluating", "complete", "failed"
    };

    private static readonly Dictionary<SprintPhase, HashSet<SprintPhase>> AllowedTransitions = new()
    {
        [Planning] = new() { Executing },
        [Executing] = new() { Evaluating },
        [Evaluating] = new() { Complete, Failed },
        [Complete] = new() { },
        [Failed] = new() { },
    };

    public string Value { get; }

    private SprintPhase(string value) => Value = value;

    public static SprintPhase FromString(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid phase '{value}'. Must be one of: planning, executing, evaluating, complete, failed", nameof(value));
        return new SprintPhase(value.ToLowerInvariant());
    }

    public bool CanTransitionTo(SprintPhase target) =>
        AllowedTransitions.TryGetValue(this, out var allowed) && allowed.Contains(target);

    public SprintPhase Next()
    {
        return this switch
        {
            var p when p == Planning => Executing,
            var p when p == Executing => Evaluating,
            _ => throw new InvalidOperationException($"No next phase from '{Value}'.")
        };
    }

    public bool IsTerminal => Value is "complete" or "failed";

    public override string ToString() => Value;
}
