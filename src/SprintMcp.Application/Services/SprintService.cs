using System.Text.Json;
using Microsoft.Extensions.Logging;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Services;

public class SprintService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ITicketRepository _ticketRepo;
    private readonly ISprintRepository _sprintRepo;
    private readonly ISprintHandoffRepository _handoffRepo;
    private readonly IActiveTaskRepository _activeTaskRepo;
    private readonly IEvalReportRepository _evalReportRepo;
    private readonly ISubagentRunChecker _runChecker;
    private readonly ITransactionManager _txManager;
    private readonly IEventStore _eventStore;
    private readonly InvariantEngine _invariantEngine;
    private readonly ILogger<SprintService> _logger;
    private readonly string _projectRoot;
    private readonly TimeProvider _timeProvider;
    private readonly ISprintLock _sprintLock;

    public SprintService(
        ITicketRepository ticketRepo,
        ISprintRepository sprintRepo,
        ISprintHandoffRepository handoffRepo,
        IActiveTaskRepository activeTaskRepo,
        IEvalReportRepository evalReportRepo,
        ISubagentRunChecker runChecker,
        ITransactionManager txManager,
        IEventStore eventStore,
        InvariantEngine invariantEngine,
        ILogger<SprintService> logger,
        string projectRoot,
        TimeProvider timeProvider,
        ISprintLock sprintLock)
    {
        _ticketRepo = ticketRepo;
        _sprintRepo = sprintRepo;
        _handoffRepo = handoffRepo;
        _activeTaskRepo = activeTaskRepo;
        _evalReportRepo = evalReportRepo;
        _runChecker = runChecker;
        _txManager = txManager;
        _eventStore = eventStore;
        _invariantEngine = invariantEngine;
        _logger = logger;
        _projectRoot = projectRoot;
        _timeProvider = timeProvider;
        _sprintLock = sprintLock;
    }

    private static Event DomainEvent(string eventType, string aggregateType, string aggregateId,
        string[]? causedBy, DateTime occurredAt, object payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        return new Event(eventType, "domain", aggregateType, aggregateId, null, causedBy ?? [], occurredAt, payloadJson);
    }

    private async Task<(ToolResult? Error, Sprint? Sprint)> ResolveSprintAsync(string? sprintId, CancellationToken ct)
    {
        if (sprintId is not null)
        {
            var sprint = await _sprintRepo.GetByIdAsync(sprintId, ct);
            if (sprint is null)
                return (ToolResult.Error($"Sprint '{sprintId}' not found"), null);
            return (null, sprint);
        }

        var active = await _sprintRepo.GetAllActiveAsync(ct);
        if (active.Count == 0)
            return (ToolResult.Error("No active sprint"), null);
        if (active.Count > 1)
            return (ToolResult.Error("Multiple active sprints. Specify sprint_id."), null);
        return (null, active[0]);
    }

    public async Task<ToolResult> UpdateHandoffAsync(string? currentFocus, string? inProgress, string? discoveries, string? nextSteps, string[]? causedBy = null, string? sprintId = null, CancellationToken ct = default)
    {
        var (resolveErr, sprint) = await ResolveSprintAsync(sprintId, ct);
        if (resolveErr is not null) return resolveErr;

        await _sprintLock.WaitAsync(sprint!.Id, ct);
        try
        {
            if (currentFocus?.Length > FieldLimits.HandoffFieldMax)
                return ToolResult.Error($"Current focus exceeds {FieldLimits.HandoffFieldMax} characters");
            if (inProgress?.Length > FieldLimits.HandoffFieldMax)
                return ToolResult.Error($"In progress exceeds {FieldLimits.HandoffFieldMax} characters");
            if (discoveries?.Length > FieldLimits.HandoffFieldMax)
                return ToolResult.Error($"Discoveries exceeds {FieldLimits.HandoffFieldMax} characters");
            if (nextSteps?.Length > FieldLimits.HandoffFieldMax)
                return ToolResult.Error($"Next steps exceeds {FieldLimits.HandoffFieldMax} characters");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var handoff = await _handoffRepo.GetBySprintIdAsync(sprint.Id, ct)
                ?? new SprintHandoff(sprint.Id);

            if (currentFocus is not null) handoff.UpdateFocus(currentFocus, now);
            if (inProgress is not null) handoff.UpdateInProgress(inProgress, now);
            if (discoveries is not null) handoff.UpdateDiscoveries(discoveries, now);
            if (nextSteps is not null) handoff.UpdateNextSteps(nextSteps, now);

            var evt = DomainEvent("SprintHandoffUpdated", "sprint", sprint.Id, causedBy, now,
                new { sprint_id = sprint.Id });
            _eventStore.Track(evt);

            await _handoffRepo.UpsertAsync(handoff, ct);

            return ToolResult.Ok(new HandoffUpdatedResponse(
                sprint.Id,
                handoff.CurrentFocus,
                handoff.InProgress,
                handoff.Discoveries,
                handoff.NextSteps
            ), evt.Id);
        }
        finally
        {
            _sprintLock.Release(sprint.Id);
        }
    }

    public async Task<ToolResult> AddActiveTaskAsync(string taskRef, string[]? causedBy = null, string? sprintId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(taskRef))
            return ToolResult.Error("Task reference cannot be empty");
        if (taskRef.Length > FieldLimits.ActiveTaskRefMax)
            return ToolResult.Error($"Task reference exceeds {FieldLimits.ActiveTaskRefMax} characters");

        var (resolveErr, sprint) = await ResolveSprintAsync(sprintId, ct);
        if (resolveErr is not null) return resolveErr;

        await _sprintLock.WaitAsync(sprint!.Id, ct);
        try
        {
            var tasks = await _activeTaskRepo.GetBySprintIdAsync(sprint.Id, ct);
            var maxOrdinal = tasks.Count > 0 ? tasks.Max(t => t.Ordinal) : 0;

            var task = new ActiveTask(sprint.Id, taskRef, maxOrdinal + 1);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var evt = DomainEvent("ActiveTaskAdded", "sprint", sprint.Id, causedBy, now,
                new { sprint_id = sprint.Id, task_ref = taskRef, ordinal = task.Ordinal });
            _eventStore.Track(evt);

            await _activeTaskRepo.AddAsync(task, ct);

            return ToolResult.Ok(new TaskAddedResponse(sprint.Id, task.Id, taskRef), evt.Id);
        }
        finally
        {
            _sprintLock.Release(sprint.Id);
        }
    }

    public async Task<ToolResult> RemoveActiveTaskAsync(int taskId, string[]? causedBy = null, string? sprintId = null, CancellationToken ct = default)
    {
        if (taskId <= 0)
            return ToolResult.Error("Invalid task ID");

        var (resolveErr, sprint) = await ResolveSprintAsync(sprintId, ct);
        if (resolveErr is not null) return resolveErr;

        await _sprintLock.WaitAsync(sprint!.Id, ct);
        try
        {
            var deleted = await _activeTaskRepo.DeleteBySprintIdAndIdAsync(sprint.Id, taskId, ct);
            if (!deleted)
                return ToolResult.Error($"Task {taskId} not found in active sprint");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var evt = DomainEvent("ActiveTaskRemoved", "sprint", sprint.Id, causedBy, now,
                new { sprint_id = sprint.Id, task_id = taskId });
            var saved = await _eventStore.AppendAsync(evt, ct);

            return ToolResult.Ok(new TaskRemovedResponse(sprint.Id, taskId), saved.Id);
        }
        finally
        {
            _sprintLock.Release(sprint.Id);
        }
    }

    public async Task<ToolResult> StartSprintAsync(string? title, string? ticketId, string priority, string[]? causedBy = null, CancellationToken ct = default)
    {
        await using var tx = await _txManager.BeginAsync(ct);

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
            if (title.Length > FieldLimits.TitleMax)
                return ToolResult.Error($"Title exceeds {FieldLimits.TitleMax} characters");
            if (!Priority.TryFromString(priority, out var parsedPrio))
                return ToolResult.Error($"Invalid priority '{priority}'");
            ticket = await _ticketRepo.CreateAsync(title, title, parsedPrio!, ct);
            tid = ticket.Id;
        }
        else
        {
            return ToolResult.Error("Provide title or ticket_id");
        }

        var sprint = await _sprintRepo.CreateNextAsync(ct);
        var sprintId = sprint.Id;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        ticket.AssignToSprint(sprintId);
        ticket.Touch(now);

        var evt = DomainEvent("SprintStarted", "sprint", sprintId, causedBy, now,
            new { sprint_id = sprintId, ticket_id = tid });
        _eventStore.Track(evt);

        await _ticketRepo.UpdateAsync(ticket, ct);

        await tx.CommitAsync(ct);

        return ToolResult.Ok(new SprintStartedResponse(tid, sprintId, $"Sprint started: {sprintId}"), evt.Id);
    }

    public async Task<ToolResult> GetBoardAsync(string? sprintId = null, CancellationToken ct = default)
    {
        var (resolveErr, sprint) = await ResolveSprintAsync(sprintId, ct);
        if (resolveErr is not null) return resolveErr;

        var tickets = await _ticketRepo.GetBySprintIdAsync(sprint!.Id, ct);
        var handoff = await _handoffRepo.GetBySprintIdAsync(sprint.Id, ct);
        var activeTasks = await _activeTaskRepo.GetBySprintIdAsync(sprint.Id, ct);

        var ticketList = tickets.Select(t => new TicketBoardDto(
            t.Id, t.Title, t.Status.Value, t.Priority.Value, t.Tier.Value, t.PlanApprovedAt.HasValue
        )).ToList();

        var (lockHeld, lockSince) = _sprintLock.Snapshot(sprint.Id);

        return ToolResult.Ok(new SprintBoardResponse(
            sprint.Id,
            sprint.Phase.Value,
            sprint.Status.Value,
            lockHeld,
            lockHeld ? lockSince.ToString("O") : "",
            ticketList,
            handoff is not null
                ? new HandoffDto(handoff.CurrentFocus, handoff.InProgress, handoff.Discoveries, handoff.NextSteps)
                : null,
            activeTasks.Select(t => new ActiveTaskDto(t.Id, t.TaskRef, t.Ordinal)).ToList()
        ));
    }

    public async Task<ToolResult> AdvancePhaseAsync(string[]? causedBy = null, string? sprintId = null, CancellationToken ct = default)
    {
        var (resolveErr, sprint) = await ResolveSprintAsync(sprintId, ct);
        if (resolveErr is not null) return resolveErr;

        await _sprintLock.WaitAsync(sprint!.Id, ct);
        try
        {
            var fromPhase = sprint.Phase.Value;
            try
            {
                sprint.AdvancePhase();
            }
            catch (InvalidOperationException ex)
            {
                return ToolResult.Error(ex.Message);
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var evt = DomainEvent("PhaseAdvanced", "sprint", sprint.Id, causedBy, now,
                new { sprint_id = sprint.Id, from = fromPhase, to = sprint.Phase.Value });
            _eventStore.Track(evt);

            await _sprintRepo.UpdateAsync(sprint, ct);

            return ToolResult.Ok(new SprintAdvancedResponse(sprint.Id, sprint.Phase.Value), evt.Id);
        }
        finally
        {
            _sprintLock.Release(sprint.Id);
        }
    }

    public async Task<ToolResult> CloseSprintAsync(string[]? causedBy = null, string? sprintId = null, CancellationToken ct = default)
    {
        var (resolveErr, sprint) = await ResolveSprintAsync(sprintId, ct);
        if (resolveErr is not null) return resolveErr;

        await _sprintLock.WaitAsync(sprint!.Id, ct);
        try
        {
            if (sprint.Phase != SprintPhase.Evaluating)
                return ToolResult.Error($"Cannot close sprint in phase '{sprint.Phase}'. Sprint must be in evaluating phase.");

            var tickets = await _ticketRepo.GetBySprintIdAsync(sprint.Id, ct);
            if (tickets.Count == 0)
                return ToolResult.Error("No tickets in sprint. Nothing to close.");

            var nonTrivial = tickets.Where(t => t.Tier != TicketTier.Trivial).ToList();
            var incomplete = nonTrivial.Where(t => !t.Status.IsTerminal).Select(t => t.Id).ToList();
            if (incomplete.Count > 0)
                return ToolResult.Error($"Cannot close sprint. Non-terminal tickets: {string.Join(", ", incomplete)}");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var matchedReports = new List<(EvalReport report, long epoch)>();
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

                matchedReports.Add((report, epoch));
            }

            await using var tx = await _txManager.BeginAsync(ct);
            foreach (var (report, epoch) in matchedReports)
            {
                report.MarkRunMatched(DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime, now);
                await _evalReportRepo.UpsertAsync(report, ct);
            }

            sprint.Close(now);

            var evt = DomainEvent("SprintClosed", "sprint", sprint.Id, causedBy, now,
                new { sprint_id = sprint.Id, ticket_count = tickets.Count });
            _eventStore.Track(evt);

            await _sprintRepo.UpdateAsync(sprint, ct);
            SprintLog.Closed(_logger, sprint.Id, tickets.Count);

            await tx.CommitAsync(ct);

            return ToolResult.Ok(new SprintClosedResponse(
                "Sprint closed successfully.",
                sprint.Id,
                tickets.Select(t => new SprintReceiptItem(t.Id, t.Status.Value, t.Title)).ToList()
            ), evt.Id);
        }
        finally
        {
            _sprintLock.Release(sprint.Id);
        }
    }
}
