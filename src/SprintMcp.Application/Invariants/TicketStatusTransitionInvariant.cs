using System.Text.Json;
using SprintMcp.Application.Abstractions;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Invariants;

public class TicketStatusTransitionInvariant : IInvariant
{
    public string Name => "TicketStatusTransition";

    public bool AppliesTo(Event proposal) =>
        proposal.EventType == "TicketStatusChanged";

    public async ValueTask<CheckResult> CheckAsync(Event proposal, IInvariantContext context, CancellationToken ct)
    {
        string? fromStatus = null;
        string? toStatus = null;
        string? ticketId = null;

        try
        {
            using var doc = JsonDocument.Parse(proposal.EventData);
            if (doc.RootElement.TryGetProperty("from", out var fromEl))
                fromStatus = fromEl.GetString();
            if (doc.RootElement.TryGetProperty("to", out var toEl))
                toStatus = toEl.GetString();
            if (doc.RootElement.TryGetProperty("ticket_id", out var idEl))
                ticketId = idEl.GetString();
        }
        catch
        {
            return CheckResult.Fail("Invalid EventData payload");
        }

        if (string.IsNullOrEmpty(fromStatus) || string.IsNullOrEmpty(toStatus))
            return CheckResult.Fail("Missing 'from' or 'to' in payload");
        if (string.IsNullOrEmpty(ticketId))
            return CheckResult.Fail("Missing 'ticket_id' in payload");

        TicketStatus from;
        TicketStatus to;
        try
        {
            from = TicketStatus.FromString(fromStatus);
            to = TicketStatus.FromString(toStatus);
        }
        catch (ArgumentException ex)
        {
            return CheckResult.Fail(ex.Message);
        }

        if (!from.CanTransitionTo(to))
            return CheckResult.Fail($"Invalid transition: '{fromStatus}' -> '{toStatus}'");

        var currentStatus = await context.GetTicketStatusAsync(ticketId, ct);
        if (currentStatus is not null && currentStatus != fromStatus)
            return CheckResult.Fail($"Payload 'from' ({fromStatus}) does not match current ticket status ({currentStatus})");

        return CheckResult.Pass();
    }
}
