using System.Text.Json;
using SprintMcp.Application.Abstractions;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Invariants;

public class PhaseGateInvariant : IInvariant
{
    public string Name => "PhaseGate";

    public bool AppliesTo(Event proposal) =>
        EventTypeRegistry.IsExecutionGated(proposal.EventType) || proposal.EventType == "ToolResult";

    public async ValueTask<CheckResult> CheckAsync(Event proposal, IInvariantContext context, CancellationToken ct)
    {
        bool needsExecution;

        if (proposal.EventType == "ToolResult")
        {
            try
            {
                using var doc = JsonDocument.Parse(proposal.EventData);
                needsExecution = doc.RootElement.TryGetProperty("is_write", out var writeProp)
                    && writeProp.ValueKind == JsonValueKind.True;
            }
            catch
            {
                needsExecution = false;
            }
        }
        else
        {
            needsExecution = true;
        }

        if (!needsExecution)
            return CheckResult.Pass();

        var phase = await context.GetActiveSprintPhaseAsync(ct);
        if (phase is null)
            return CheckResult.Fail("No active sprint");
        if (phase != SprintPhase.Executing.Value)
            return CheckResult.Fail($"{proposal.EventType} not allowed before Execution phase (current: {phase})");

        return CheckResult.Pass();
    }
}
