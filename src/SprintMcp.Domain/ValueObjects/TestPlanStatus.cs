namespace SprintMcp.Domain.ValueObjects;

public record TestPlanStatus
{
    public static readonly TestPlanStatus Pending = new("pending");
    public static readonly TestPlanStatus Pass = new("pass");
    public static readonly TestPlanStatus Fail = new("fail");
    public static readonly TestPlanStatus Blocked = new("blocked");

    private static readonly HashSet<string> ValidValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending", "pass", "fail", "blocked"
    };

    public string Value { get; }

    private TestPlanStatus(string value) => Value = value;

    public static TestPlanStatus FromString(string value)
    {
        if (!ValidValues.Contains(value))
            throw new ArgumentException($"Invalid test plan status '{value}'. Must be one of: pending, pass, fail, blocked", nameof(value));
        return new TestPlanStatus(value.ToLowerInvariant());
    }

    public override string ToString() => Value;
}
