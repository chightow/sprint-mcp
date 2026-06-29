using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Services;

public class SprintService
{
    private readonly ITicketRepository _ticketRepo;
    private readonly ISprintRepository _sprintRepo;
    private readonly ISprintHandoffRepository _handoffRepo;
    private readonly IActiveTaskRepository _activeTaskRepo;
    private readonly IEvalReportRepository _evalReportRepo;
    private readonly ISubagentRunChecker _runChecker;
    private readonly string _projectRoot;

    public SprintService(
        ITicketRepository ticketRepo,
        ISprintRepository sprintRepo,
        ISprintHandoffRepository handoffRepo,
        IActiveTaskRepository activeTaskRepo,
        IEvalReportRepository evalReportRepo,
        ISubagentRunChecker runChecker,
        string projectRoot)
    {
        _ticketRepo = ticketRepo;
        _sprintRepo = sprintRepo;
        _handoffRepo = handoffRepo;
        _activeTaskRepo = activeTaskRepo;
        _evalReportRepo = evalReportRepo;
        _runChecker = runChecker;
        _projectRoot = projectRoot;
    }

    public async Task<ToolResult> StartSprintAsync(string? title, string? ticketId, string priority)
    {
        var active = await _sprintRepo.GetActiveAsync();
        if (active is not null)
            return ToolResult.Error("An active sprint already exists. Close it first.");

        string tid;
        if (ticketId is not null)
        {
            var existing = await _ticketRepo.GetByIdAsync(ticketId);
            if (existing is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");
            tid = ticketId;
        }
        else if (title is not null)
        {
            var created = await _ticketRepo.CreateAsync(title, title, Priority.FromString(priority));
            tid = created.Id;
        }
        else
        {
            return ToolResult.Error("Provide title or ticket_id");
        }

        var sprintId = await _sprintRepo.GetNextIdAsync();
        await _sprintRepo.CreateAsync(sprintId);

        var ticket = await _ticketRepo.GetByIdAsync(tid);
        if (ticket is not null)
        {
            ticket.SprintId = sprintId;
            await _ticketRepo.UpdateAsync(ticket);
        }

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = tid,
            ["sprint_id"] = sprintId,
            ["message"] = $"Sprint started: {sprintId}"
        });
    }

    public async Task<ToolResult> GetBoardAsync()
    {
        var active = await _sprintRepo.GetActiveAsync();
        if (active is null)
            return ToolResult.Error("No active sprint");

        var tickets = await _ticketRepo.GetBySprintIdAsync(active.Id);
        var handoff = await _handoffRepo.GetBySprintIdAsync(active.Id);
        var activeTasks = await _activeTaskRepo.GetBySprintIdAsync(active.Id);

        var ticketList = tickets.Select(t => new Dictionary<string, object>
        {
            ["id"] = t.Id,
            ["title"] = t.Title,
            ["status"] = t.Status.Value,
            ["priority"] = t.Priority.Value,
            ["tier"] = t.Tier,
            ["plan_approved"] = t.PlanApprovedAt.HasValue
        }).ToList<object>();

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["sprint_id"] = active.Id,
            ["tickets"] = ticketList,
            ["handoff"] = handoff is not null ? new Dictionary<string, object>
            {
                ["current_focus"] = handoff.CurrentFocus,
                ["in_progress"] = handoff.InProgress,
                ["discoveries"] = handoff.Discoveries,
                ["next_steps"] = handoff.NextSteps
            } : null!,
            ["active_tasks"] = activeTasks.Select(t => new Dictionary<string, object>
            {
                ["id"] = t.Id,
                ["task_ref"] = t.TaskRef,
                ["ordinal"] = t.Ordinal
            }).ToList<object>()
        });
    }

    public async Task<ToolResult> CloseSprintAsync()
    {
        var active = await _sprintRepo.GetActiveAsync();
        if (active is null)
            return ToolResult.Error("No active sprint to close");

        var tickets = await _ticketRepo.GetBySprintIdAsync(active.Id);
        if (tickets.Count == 0)
            return ToolResult.Error("No tickets in sprint. Nothing to close.");

        var nonTrivial = tickets.Where(t => !string.Equals(t.Tier, "trivial", StringComparison.OrdinalIgnoreCase)).ToList();
        var terminalStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "closed", "cancelled", "archived" };

        var incomplete = nonTrivial.Where(t => !terminalStatuses.Contains(t.Status.Value)).Select(t => t.Id).ToList();
        if (incomplete.Count > 0)
            return ToolResult.Error($"Cannot close sprint. Non-terminal tickets: {string.Join(", ", incomplete)}");

        foreach (var t in nonTrivial)
        {
            var report = await _evalReportRepo.GetByTicketIdAsync(t.Id);
            if (report is null)
                return ToolResult.Error($"Ticket {t.Id} cannot close: eval report missing.");

            if (!string.Equals(report.Verdict, "pass", StringComparison.OrdinalIgnoreCase))
                return ToolResult.Error($"Ticket {t.Id} cannot close: verdict is '{report.Verdict}', not 'pass'.");

            var parts = report.RunId.Split('-', 3);
            if (parts.Length < 2 || !long.TryParse(parts[0], out var epoch))
                return ToolResult.Error($"Ticket {t.Id} cannot close: invalid run-id '{report.RunId}'.");

            if (!_runChecker.CheckRun(epoch, _projectRoot))
                return ToolResult.Error($"Ticket {t.Id} cannot close: run-id '{report.RunId}' has no matching subagent entry.");

            report.MatchedRunTs = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime.ToString("O");
            await _evalReportRepo.UpsertAsync(report);
        }

        active.Status = "closed";
        active.ClosedAt = DateTime.UtcNow;
        await _sprintRepo.UpdateAsync(active);

        var receiptLines = new List<string>
        {
            "## Delivery Receipt",
            "| Ticket ID | Status | Title |",
            "| --- | --- | --- |"
        };
        foreach (var t in tickets)
        {
            var esc = (string s) => s.Replace("|", "\\|");
            receiptLines.Add($"| {esc(t.Id)} | {esc(t.Status.Value)} | {esc(t.Title)} |");
        }

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["message"] = "Sprint closed successfully.",
            ["sprint_id"] = active.Id,
            ["receipt"] = string.Join("\n", receiptLines)
        });
    }
}
