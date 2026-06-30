namespace SprintMcp.Domain.ValueObjects;

public record Verdict
{
    public static readonly Verdict Pass = new("pass");
    public static readonly Verdict Fail = new("fail");
    public static readonly Verdict Pending = new("pending");

    private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "pass", "fail", "pending"
    };

    public string Value { get; }

    private Verdict(string value) => Value = value;

    public static Verdict FromString(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid verdict '{value}'. Must be one of: pass, fail, pending", nameof(value));
        return new Verdict(value.ToLowerInvariant());
    }

    public bool IsTerminal => Value is "pass" or "fail";

    public override string ToString() => Value;
}
