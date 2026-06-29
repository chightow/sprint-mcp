using System.Text.RegularExpressions;

namespace SprintMcp.Domain.ValueObjects;

public partial record TicketId
{
    private static readonly Regex ValidPattern = ValidTicketIdRegex();

    [GeneratedRegex(@"^TKT-\d{4,}$")]
    private static partial Regex ValidTicketIdRegex();

    public string Value { get; }

    public TicketId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        if (!ValidPattern.IsMatch(value))
            throw new ArgumentException($"Invalid ticket ID format: {value} (expected TKT-XXXX)", nameof(value));
        Value = value;
    }

    public static TicketId FromString(string value) => new(value);
    public override string ToString() => Value;
}
