using System.Text.RegularExpressions;

namespace SprintMcp.Domain.ValueObjects;

public partial record SprintId
{
    private static readonly Regex ValidPattern = ValidSprintIdRegex();

    [GeneratedRegex(@"^SPRINT-\d{4,}$")]
    private static partial Regex ValidSprintIdRegex();

    public string Value { get; }

    public SprintId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        if (!ValidPattern.IsMatch(value))
            throw new ArgumentException($"Invalid sprint ID format: {value} (expected SPRINT-XXXX)", nameof(value));
        Value = value;
    }

    public static void Validate(string value) => _ = new SprintId(value);
    public static SprintId FromString(string value) => new(value);
    public override string ToString() => Value;
}
