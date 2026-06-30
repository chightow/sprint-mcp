using SprintMcp.Domain.Entities;

namespace SprintMcp.Application.Abstractions;

public sealed record CheckResult(bool Valid, string? Failure = null)
{
    public static CheckResult Pass() => new(true, null);
    public static CheckResult Fail(string reason) => new(false, reason);
}

public interface IInvariant
{
    string Name { get; }
    bool AppliesTo(Event proposal);
    ValueTask<CheckResult> CheckAsync(Event proposal, IInvariantContext context, CancellationToken ct);
}

public interface IInvariantContext
{
    Task<string?> GetActiveSprintPhaseAsync(CancellationToken ct);
    Task<string?> GetTicketStatusAsync(string ticketId, CancellationToken ct);
}
