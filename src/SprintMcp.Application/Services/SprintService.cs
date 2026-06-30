using Microsoft.Extensions.Logging;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Services;

public class SprintService
{
    private static readonly SemaphoreSlim _sprintLock = new(1, 1);

    private readonly ITicketRepository _ticketRepo;
    private readonly ISprintRepository _sprintRepo;
    private readonly ISprintHandoffRepository _handoffRepo;
    private readonly IActiveTaskRepository _activeTaskRepo;
    private readonly IEvalReportRepository _evalReportRepo;
    private readonly ISubagentRunChecker _runChecker;
    private readonly ITransactionManager _txManager;
    private readonly ILogger<SprintService> _logger;
    private readonly string _projectRoot;

    public async Task<ToolResult> UpdateHandoffAsync(string? currentFocus, string? inProgress, string? discoveries, string? nextSteps, CancellationToken ct = default)
    {
        await _sprintLock.WaitAsync(ct);
        try
        {
        var active = await _sprintRepo.GetActiveAsync(ct);
        if (active is null)
            return ToolResult.Error("No active sprint");

        var handoff = await _handoffRepo.GetBySprintIdAsync(active.Id, ct)
            ?? new SprintHandoff { SprintId = active.Id };

        if (currentFocus is not null) handoff.CurrentFocus = currentFocus;
        if (inProgress is not null) handoff.InProgress = inProgress;
        if (discoveries is not null) handoff.Discoveries = discoveries;
        if (nextSteps is not null) handoff.NextSteps = nextSteps;

        await _handoffRepo.UpsertAsync(handoff, ct);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["sprint_id"] = active.Id,
            ["current_focus"] = handoff.CurrentFocus,
            ["in_progress"] = handoff.InProgress,
            ["discoveries"] = handoff.Discoveries,
            ["next_steps"] = handoff.NextSteps
        });
        }
        finally
        {
            _sprintLock.Release();
        }
    }

    public async Task<ToolResult> AddActiveTaskAsync(string taskRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskRef))
            return ToolResult.Error("Task reference cannot be empty");

        await _sprintLock.WaitAsync(ct);
        try
        {
        var active = await _sprintRepo.GetActiveAsync(ct);
        if (active is null)
            return ToolResult.Error("No active sprint");

        var tasks = await _activeTaskRepo.GetBySprintIdAsync(active.Id, ct);
        var maxOrdinal = tasks.Count > 0 ? tasks.Max(t => t.Ordinal) : 0;

        var task = new ActiveTask
        {
            SprintId = active.Id,
            TaskRef = taskRef,
            Ordinal = maxOrdinal + 1
        };
        await _activeTaskRepo.AddAsync(task, ct);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["sprint_id"] = active.Id,
            ["task_id"] = task.Id,
            ["task_ref"] = taskRef
        });
        }
        finally
        {
            _sprintLock.Release();
        }
    }

    public async Task<ToolResult> RemoveActiveTaskAsync(int taskId, CancellationToken ct = default)
    {
        if (taskId <= 0)
            return ToolResult.Error("Invalid task ID");

        await _sprintLock.WaitAsync(ct);
        try
        {
        var active = await _sprintRepo.GetActiveAsync(ct);
        if (active is null)
            return ToolResult.Error("No active sprint");

        var task = await _activeTaskRepo.GetByIdAsync(taskId, ct);
        if (task is null || task.SprintId != active.Id)
            return ToolResult.Error($"Task {taskId} not found in active sprint");

        await _activeTaskRepo.DeleteByIdAsync(taskId, ct);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["sprint_id"] = active.Id,
            ["removed_task_id"] = taskId
        });
        }
        finally
        {
            _sprintLock.Release();
        }
    }

    public SprintService(
        ITicketRepository ticketRepo,
        ISprintRepository sprintRepo,
        ISprintHandoffRepository handoffRepo,
        IActiveTaskRepository activeTaskRepo,
        IEvalReportRepository evalReportRepo,
        ISubagentRunChecker runChecker,
        ITransactionManager txManager,
        ILogger<SprintService> logger,
        string projectRoot)
    {
        _ticketRepo = ticketRepo;
        _sprintRepo = sprintRepo;
        _handoffRepo = handoffRepo;
        _activeTaskRepo = activeTaskRepo;
        _evalReportRepo = evalReportRepo;
        _runChecker = runChecker;
        _txManager = txManager;
        _logger = logger;
        _projectRoot = projectRoot;
    }

    private static bool TryParsePriority(string priority, out Priority result)
    {
        try
        {
            result = Priority.FromString(priority);
            return true;
        }
        catch (ArgumentException)
        {
            result = Priority.Default;
            return false;
        }
    }

    public async Task<ToolResult> StartSprintAsync(string? title, string? ticketId, string priority, CancellationToken ct = default)
    {
        await _sprintLock.WaitAsync(ct);
        try
        {
            await using var tx = await _txManager.BeginAsync(ct);

            var active = await _sprintRepo.GetActiveAsync(ct);
            if (active is not null)
                return ToolResult.Error("An active sprint already exists. Close it first.");

            Ticket ticket;
            string tid;
            if (ticketId is not null)
            {
                var existing = await _ticketRepo.GetByIdAsync(ticketId, ct);
                if (existing is null)
                    return ToolResult.Error($"Ticket '{ticketId}' not found");
                ticket = existing;
                tid = ticketId;
            }
            else if (title is not null)
            {
                if (!TryParsePriority(priority, out var parsedPrio))
                    return ToolResult.Error($"Invalid priority '{priority}'");
                ticket = await _ticketRepo.CreateAsync(title, title, parsedPrio, ct);
                tid = ticket.Id;
            }
            else
            {
                return ToolResult.Error("Provide title or ticket_id");
            }

            var sprintId = await _sprintRepo.GetNextIdAsync(ct);
            await _sprintRepo.CreateAsync(sprintId, ct);

            ticket.SprintId = sprintId;
            await _ticketRepo.UpdateAsync(ticket, ct);

            await tx.CommitAsync(ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = tid,
                ["sprint_id"] = sprintId,
                ["message"] = $"Sprint started: {sprintId}"
            });
        }
        finally
        {
            _sprintLock.Release();
        }
    }

    public async Task<ToolResult> GetBoardAsync(CancellationToken ct = default)
    {
        var active = await _sprintRepo.GetActiveAsync(ct);
        if (active is null)
            return ToolResult.Error("No active sprint");

        var tickets = await _ticketRepo.GetBySprintIdAsync(active.Id, ct);
        var handoff = await _handoffRepo.GetBySprintIdAsync(active.Id, ct);
        var activeTasks = await _activeTaskRepo.GetBySprintIdAsync(active.Id, ct);

        var ticketList = tickets.Select(t => new Dictionary<string, object>
        {
            ["id"] = t.Id,
            ["title"] = t.Title,
            ["status"] = t.Status.Value,
            ["priority"] = t.Priority.Value,
            ["tier"] = t.Tier.Value,
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

    public async Task<ToolResult> CloseSprintAsync(CancellationToken ct = default)
    {
        await _sprintLock.WaitAsync(ct);
        try
        {
            await using var tx = await _txManager.BeginAsync(ct);

            var active = await _sprintRepo.GetActiveAsync(ct);
            if (active is null)
                return ToolResult.Error("No active sprint to close");

            var tickets = await _ticketRepo.GetBySprintIdAsync(active.Id, ct);
            if (tickets.Count == 0)
                return ToolResult.Error("No tickets in sprint. Nothing to close.");

            var nonTrivial = tickets.Where(t => t.Tier != TicketTier.Trivial).ToList();
            var terminalStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "closed", "cancelled", "archived" };

            var incomplete = nonTrivial.Where(t => !terminalStatuses.Contains(t.Status.Value)).Select(t => t.Id).ToList();
            if (incomplete.Count > 0)
                return ToolResult.Error($"Cannot close sprint. Non-terminal tickets: {string.Join(", ", incomplete)}");

            foreach (var t in nonTrivial)
            {
                var report = await _evalReportRepo.GetByTicketIdAsync(t.Id, ct);
                if (report is null)
                    return ToolResult.Error($"Ticket {t.Id} cannot close: eval report missing.");

                if (report.Verdict != Verdict.Pass)
                    return ToolResult.Error($"Ticket {t.Id} cannot close: verdict is '{report.Verdict}', not 'pass'.");

                if (report.MatchedRunTs is not null)
                    continue;

                var parts = report.RunId.Split('-', 3);
                if (parts.Length < 2 || !long.TryParse(parts[0], out var epoch))
                    return ToolResult.Error($"Ticket {t.Id} cannot close: invalid run-id '{report.RunId}'.");

                if (!await _runChecker.CheckRunAsync(epoch, _projectRoot, ct))
                    return ToolResult.Error($"Ticket {t.Id} cannot close: run-id '{report.RunId}' has no matching subagent entry.");

                report.MatchedRunTs = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime.ToString("O");
                await _evalReportRepo.UpsertAsync(report, ct);
            }

            active.Close();
            await _sprintRepo.UpdateAsync(active, ct);
            _logger.LogInformation("Sprint {SprintId} closed with {TicketCount} tickets", active.Id, tickets.Count);

            await tx.CommitAsync(ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["message"] = "Sprint closed successfully.",
                ["sprint_id"] = active.Id,
                ["receipt"] = tickets.Select(t => new Dictionary<string, object>
                {
                    ["ticket_id"] = t.Id,
                    ["status"] = t.Status.Value,
                    ["title"] = t.Title
                }).ToList<object>()
            });
        }
        finally
        {
            _sprintLock.Release();
        }
    }
}
