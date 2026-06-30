using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Services;

public partial class TicketService
{
    public const int MaxTicketsPerSprint = 50;

    private static readonly Regex RunIdPattern = RunIdRegex();

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new SprintIdJsonConverter(), new TicketIdJsonConverter() }
    };

    private readonly TicketServiceContext _ctx;

    public TicketService(TicketServiceContext ctx)
    {
        _ctx = ctx;
    }

    [GeneratedRegex(@"^\d{1,10}-.+$")]
    private static partial Regex RunIdRegex();

    private static Event DomainEvent(string eventType, string aggregateType, object aggregateId,
        string[]? causedBy, DateTime occurredAt, object payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, PayloadJsonOptions);
        return new Event(eventType, "domain", aggregateType, aggregateId.ToString()!, null, causedBy ?? [], occurredAt, payloadJson);
    }

    private async Task<ToolResult> AppendAndWrapAsync(ToolResult result, Event evt, CancellationToken ct)
    {
        var check = await _ctx.InvariantEngine.CheckAsync(evt, ct);
        if (!check.Valid)
            return ToolResult.Error(check.Failure!);
        var saved = await _ctx.EventStore.AppendAsync(evt, ct);
        return result with { EventId = saved.Id };
    }

    private ToolResult TrackAndWrap(ToolResult result, Event evt)
    {
        _ctx.EventStore.Track(evt);
        return result with { EventId = evt.Id };
    }

    private async Task<ToolResult?> RequirePhaseForTicketAsync(string ticketId, SprintPhase requiredPhase, CancellationToken ct)
    {
        var id = TicketId.FromString(ticketId);
        var ticket = await _ctx.TicketRepo.GetByIdAsync(id, ct);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        SprintId? sprintId = ticket.SprintId;
        if (sprintId is null)
        {
            var active = await _ctx.SprintRepo.GetAllActiveAsync(ct);
            if (active.Count == 0)
                return ToolResult.Error("Ticket not assigned to any sprint and no active sprint found");
            if (active.Count > 1)
                return ToolResult.Error("Ticket not assigned to any sprint and multiple active sprints exist.");
            sprintId = active[0].Id;
        }

        var sprint = await _ctx.SprintRepo.GetByIdAsync(sprintId!, ct);
        if (sprint is null)
            return ToolResult.Error($"Sprint '{sprintId}' not found");
        if (sprint.Status != SprintStatus.Active)
            return ToolResult.Error($"Sprint '{sprintId}' is not active");
        if (sprint.Phase != requiredPhase)
            return ToolResult.Error($"Action requires phase '{requiredPhase}', but sprint is in phase '{sprint.Phase}'.");
        return null;
    }

    private async Task<(ToolResult? Error, Sprint? Sprint)> RequirePhaseForSprintAsync(SprintId sprintId, SprintPhase requiredPhase, CancellationToken ct)
    {
        var sprint = await _ctx.SprintRepo.GetByIdAsync(sprintId, ct);
        if (sprint is null)
            return (ToolResult.Error($"Sprint '{sprintId}' not found"), null);
        if (sprint.Status != SprintStatus.Active)
            return (ToolResult.Error($"Sprint '{sprintId}' is not active"), null);
        if (sprint.Phase != requiredPhase)
            return (ToolResult.Error($"Action requires phase '{requiredPhase}', but sprint is in phase '{sprint.Phase}'."), null);
        return (null, sprint);
    }

    private async Task<(ToolResult? Error, Sprint? Sprint)> ResolveActiveSprintAsync(string? sprintId, SprintPhase requiredPhase, CancellationToken ct)
    {
        if (sprintId is not null)
        {
            SprintId id;
            try { id = SprintId.FromString(sprintId); }
            catch (ArgumentException ex) { return (ToolResult.Error(ex.Message), null); }
            return await RequirePhaseForSprintAsync(id, requiredPhase, ct);
        }

        var active = await _ctx.SprintRepo.GetAllActiveAsync(ct);
        if (active.Count == 0)
            return (ToolResult.Error("No active sprint"), null);
        if (active.Count > 1)
            return (ToolResult.Error("Multiple active sprints. Specify sprint_id."), null);
        var sprint = active[0];
        if (sprint.Phase != requiredPhase)
            return (ToolResult.Error($"Action requires phase '{requiredPhase}', but sprint is in phase '{sprint.Phase}'."), null);
        return (null, sprint);
    }

    public async Task<ToolResult> CreateTicketAsync(string title, string description, string priority, string? idempotencyKey = null, string[]? causedBy = null, string? sprintId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ToolResult.Error("Title cannot be empty");
        if (title.Length > FieldLimits.TitleMax)
            return ToolResult.Error($"Title exceeds {FieldLimits.TitleMax} characters");
        if (description?.Length > FieldLimits.DescriptionMax)
            return ToolResult.Error($"Description exceeds {FieldLimits.DescriptionMax} characters");

        Priority prio;
        try { prio = Priority.FromString(priority); }
        catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

        var (phaseError, sprint) = await ResolveActiveSprintAsync(sprintId, SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var cached = await _ctx.Idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var sprintTickets = await _ctx.TicketRepo.GetBySprintIdAsync(sprint!.Id, ct);
            if (sprintTickets.Count >= MaxTicketsPerSprint)
                return ToolResult.Error($"Sprint '{sprint.Id}' already has {MaxTicketsPerSprint} tickets. Cannot create more.");

            var ticket = await _ctx.TicketRepo.CreateAsync(title, description ?? "", prio, ct);

            var result = ToolResult.Ok(new TicketCreatedResponse(ticket.Id.Value, ticket.Title));
            await _ctx.Idempotency.StoreAsync(idempotencyKey, result, ct);
            TicketLog.Created(_ctx.Logger, ticket.Id.Value, ticket.Title);

            var evt = DomainEvent("TicketCreated", "ticket", ticket.Id, causedBy, ticket.CreatedAt,
                new { ticket_id = ticket.Id, title = ticket.Title, priority = prio.Value });
            result = await AppendAndWrapAsync(result, evt, ct);
            return result;
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> ListTicketsAsync(int skip = 0, int take = 100, CancellationToken ct = default)
    {
        var tickets = await _ctx.TicketRepo.GetAllAsync(skip, take, ct);

        return ToolResult.Ok(new TicketListResponse(
            tickets.Select(t => new TicketSummaryDto(
                t.Id.Value, t.Title, t.Status.Value, t.Priority.Value, t.Tier.Value, t.SprintId?.Value ?? ""
            )).ToList()
        ));
    }

    public async Task<ToolResult> GetTicketAsync(string ticketId, CancellationToken ct = default)
    {
        var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        var criteria = await _ctx.CriterionRepo.GetByTicketIdAsync(TicketId.FromString(ticketId), ct);
        var decisions = await _ctx.DecisionRepo.GetByTicketIdAsync(TicketId.FromString(ticketId), ct);
        var testPlan = await _ctx.TestPlanRepo.GetByTicketIdAsync(TicketId.FromString(ticketId), ct);
        var evalReport = await _ctx.EvalReportRepo.GetByTicketIdAsync(TicketId.FromString(ticketId), ct);

        return ToolResult.Ok(new TicketDetailResponse(
            ticket.Id.Value, ticket.Title, ticket.Description,
            ticket.Status.Value, ticket.Priority.Value, ticket.Tier.Value,
            ticket.SprintId?.Value ?? "",
            ticket.PlanApproach, ticket.PlanFiles,
            ticket.PlanApprovedAt?.ToString("O") ?? "",
            ticket.Summary,
            ticket.CreatedAt.ToString("O"), ticket.UpdatedAt.ToString("O"),
            criteria.Select(c => new AcceptanceCriterionDto(c.Id, c.Ordinal, c.Text, c.Satisfied)).ToList(),
            decisions.Select(d => new DecisionDto(d.Id, d.Title, d.Rationale, d.CreatedAt.ToString("O"))).ToList(),
            testPlan.Select(t => new TestPlanItemDto(t.Id, t.Ordinal, t.Description, t.Expected, t.Status.Value)).ToList(),
            evalReport is not null
                ? new EvalReportDto(evalReport.RunId, evalReport.Verdict.Value, evalReport.MatchedRunTs?.ToString("O") ?? "")
                : null
        ));
    }

    public async Task<ToolResult> UpdateStatusAsync(string ticketId, string newStatus, string[]? causedBy = null, CancellationToken ct = default)
    {
        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Executing, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            TicketStatus status;
            try { status = TicketStatus.FromString(newStatus); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            if (ticket.Status.Value == status.Value)
                return ToolResult.Ok(new TicketStatusResponse(ticketId, status.Value));

            if (!ticket.Status.CanTransitionTo(status))
                return ToolResult.Error($"Cannot transition from '{ticket.Status}' to '{status}'.");

            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            var evt = DomainEvent("TicketStatusChanged", "ticket", ticketId, causedBy, now,
                new { ticket_id = ticketId, from = ticket.Status.Value, to = status.Value });

            var check = await _ctx.InvariantEngine.CheckAsync(evt, ct);
            if (!check.Valid)
                return ToolResult.Error(check.Failure!);

            ticket.TransitionTo(status);
            ticket.Touch(now);
            _ctx.EventStore.Track(evt);
            await _ctx.TicketRepo.UpdateAsync(ticket, ct);

            TicketLog.StatusChanged(_ctx.Logger, ticketId, status.Value);

            return ToolResult.Ok(new TicketStatusResponse(ticketId, status.Value), evt.Id);
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> AddCriterionAsync(string ticketId, string criterionText, string? idempotencyKey = null, string[]? causedBy = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(criterionText))
            return ToolResult.Error("Criterion text cannot be empty");
        if (criterionText.Length > FieldLimits.CriterionTextMax)
            return ToolResult.Error($"Criterion text exceeds {FieldLimits.CriterionTextMax} characters");

        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var cached = await _ctx.Idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            var ordinal = await _ctx.CriterionRepo.GetNextOrdinalAsync(TicketId.FromString(ticketId), ct);
            var criterion = new AcceptanceCriterion(TicketId.FromString(ticketId), ordinal, criterionText);

            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            var evt = DomainEvent("CriterionAdded", "ticket", ticketId, causedBy, now,
                new { ticket_id = ticketId, ordinal, text = criterionText });
            _ctx.EventStore.Track(evt);

            await _ctx.CriterionRepo.AddAsync(criterion, ct);

            var result = ToolResult.Ok(new CriterionAddedResponse(ticketId, criterionText, ordinal), evt.Id);
            await _ctx.Idempotency.StoreAsync(idempotencyKey, result, ct);
            return result;
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> CheckCriterionAsync(string ticketId, int? criterionId, int? ordinal, bool satisfied = true, string[]? causedBy = null, CancellationToken ct = default)
    {
        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Executing, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            AcceptanceCriterion? target = null;
            if (criterionId.HasValue)
                target = await _ctx.CriterionRepo.GetByTicketIdAndIdAsync(TicketId.FromString(ticketId), criterionId.Value, ct);
            else if (ordinal.HasValue)
                target = await _ctx.CriterionRepo.GetByTicketIdAndOrdinalAsync(TicketId.FromString(ticketId), ordinal.Value, ct);

            if (target is null)
                return ToolResult.Error(!criterionId.HasValue && !ordinal.HasValue
                    ? "Provide criterion_id or ordinal"
                    : $"Criterion not found (id={criterionId}, ordinal={ordinal})");

            target.Satisfied = satisfied;

            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            var evt = DomainEvent("CriterionChecked", "ticket", ticketId, causedBy, now,
                new { ticket_id = ticketId, criterion_id = target.Id, ordinal = target.Ordinal, satisfied });
            _ctx.EventStore.Track(evt);

            await _ctx.CriterionRepo.UpdateAsync(target, ct);

            return ToolResult.Ok(new CriterionCheckedResponse(ticketId, target.Id, target.Ordinal, target.Satisfied), evt.Id);
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> SetPlanAsync(string ticketId, string? tier, string? approach, string? files, bool approve = false, string[]? causedBy = null, CancellationToken ct = default)
    {
        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (tier is not null)
            {
                TicketTier parsedTier;
                try { parsedTier = TicketTier.FromString(tier); }
                catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }
                ticket.CategorizeAs(parsedTier);
            }

            if (approach is not null)
            {
                if (approach.Length > FieldLimits.PlanApproachMax)
                    return ToolResult.Error($"Plan approach exceeds {FieldLimits.PlanApproachMax} characters");
                ticket.SetPlanApproach(approach);
            }
            if (files is not null)
            {
                if (files.Length > FieldLimits.PlanFilesMax)
                    return ToolResult.Error($"Plan files exceeds {FieldLimits.PlanFilesMax} characters");
                ticket.SetPlanFiles(files);
            }

            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            long? lastEventId = null;

            if (tier is not null || approach is not null || files is not null)
            {
                var planEvt = DomainEvent("TicketPlanSet", "ticket", ticketId, causedBy, now,
                    new { ticket_id = ticketId, tier = ticket.Tier.Value, approach = ticket.PlanApproach, files = ticket.PlanFiles });
                _ctx.EventStore.Track(planEvt);
                lastEventId = planEvt.Id;
            }

            if (approve)
            {
                ticket.ApprovePlan(now);
                var approveEvt = DomainEvent("TicketPlanApproved", "ticket", ticketId, causedBy, now,
                    new { ticket_id = ticketId });
                _ctx.EventStore.Track(approveEvt);
                lastEventId = approveEvt.Id;
            }

            ticket.Touch(now);
            await _ctx.TicketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new PlanSetResponse(ticketId, ticket.Tier.Value, ticket.PlanApprovedAt.HasValue), lastEventId);
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> AddDecisionAsync(string ticketId, string title, string rationale, string? idempotencyKey = null, string[]? causedBy = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ToolResult.Error("Decision title cannot be empty");
        if (title.Length > FieldLimits.DecisionTitleMax)
            return ToolResult.Error($"Decision title exceeds {FieldLimits.DecisionTitleMax} characters");
        if (rationale?.Length > FieldLimits.DecisionRationaleMax)
            return ToolResult.Error($"Decision rationale exceeds {FieldLimits.DecisionRationaleMax} characters");

        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var cached = await _ctx.Idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            var decision = new Decision(TicketId.FromString(ticketId), title, rationale ?? "");

            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            var evt = DomainEvent("DecisionRecorded", "ticket", ticketId, causedBy, now,
                new { ticket_id = ticketId, title, rationale = rationale ?? "" });
            _ctx.EventStore.Track(evt);

            await _ctx.DecisionRepo.AddAsync(decision, ct);

            var result = ToolResult.Ok(new DecisionAddedResponse(ticketId, title), evt.Id);
            await _ctx.Idempotency.StoreAsync(idempotencyKey, result, ct);
            return result;
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> AddTestAsync(string ticketId, string description, string expected, string[]? causedBy = null, CancellationToken ct = default)
    {
        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (string.IsNullOrWhiteSpace(description))
                return ToolResult.Error("Test description cannot be empty");
            if (description.Length > FieldLimits.TestDescriptionMax)
                return ToolResult.Error($"Test description exceeds {FieldLimits.TestDescriptionMax} characters");
            if (expected?.Length > FieldLimits.TestExpectedMax)
                return ToolResult.Error($"Test expected value exceeds {FieldLimits.TestExpectedMax} characters");

            var ordinal = await _ctx.TestPlanRepo.GetNextOrdinalAsync(TicketId.FromString(ticketId), ct);
            var item = new TestPlanItem(TicketId.FromString(ticketId), ordinal, description, expected ?? "");

            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            var evt = DomainEvent("TestPlanUpdated", "ticket", ticketId, causedBy, now,
                new { ticket_id = ticketId, ordinal, description, expected = expected ?? "", action = "added" });
            _ctx.EventStore.Track(evt);

            await _ctx.TestPlanRepo.AddAsync(item, ct);

            return ToolResult.Ok(new TestAddedResponse(ticketId, ordinal, description), evt.Id);
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> UpdateTestAsync(string ticketId, int ordinal, string status, string[]? causedBy = null, CancellationToken ct = default)
    {
        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Executing, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            TestPlanStatus testStatus;
            try { testStatus = TestPlanStatus.FromString(status); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            var item = await _ctx.TestPlanRepo.GetByTicketIdAndOrdinalAsync(TicketId.FromString(ticketId), ordinal, ct);
            if (item is null)
                return ToolResult.Error($"Test plan item {ordinal} not found for ticket {ticketId}");

            item.Status = testStatus;
            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            item.UpdatedAt = now;

            var evt = DomainEvent("TestPlanUpdated", "ticket", ticketId, causedBy, now,
                new { ticket_id = ticketId, ordinal, status = item.Status.Value, action = "updated" });
            _ctx.EventStore.Track(evt);

            await _ctx.TestPlanRepo.UpdateAsync(item, ct);

            return ToolResult.Ok(new TestUpdatedResponse(ticketId, ordinal, item.Status.Value), evt.Id);
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> SetSummaryAsync(string ticketId, string summary, string[]? causedBy = null, CancellationToken ct = default)
    {
        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Evaluating, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (summary?.Length > FieldLimits.SummaryMax)
                return ToolResult.Error($"Summary exceeds {FieldLimits.SummaryMax} characters");

            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            ticket.SetSummary(summary ?? "");
            ticket.Touch(now);

            var evt = DomainEvent("SummarySet", "ticket", ticketId, causedBy, now,
                new { ticket_id = ticketId });
            _ctx.EventStore.Track(evt);

            await _ctx.TicketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new SummarySetResponse(ticketId), evt.Id);
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> SetEvalAsync(string ticketId, string runId, string verdict, string content, string? idempotencyKey = null, string[]? causedBy = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(runId) || !RunIdPattern.IsMatch(runId))
            return ToolResult.Error($"Invalid run ID '{runId}'. Expected format: <epoch>-<slug>");

        var parts = runId.Split('-', 3);
        if (!long.TryParse(parts[0], out var epoch))
            return ToolResult.Error($"Invalid run ID '{runId}': epoch must be numeric");

        var phaseError = await RequirePhaseForTicketAsync(ticketId, SprintPhase.Evaluating, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var cached = await _ctx.Idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var ticket = await _ctx.TicketRepo.GetByIdAsync(TicketId.FromString(ticketId), ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (!await _ctx.RunChecker.CheckRunAsync(epoch, _ctx.ProjectRoot, ct))
                return ToolResult.Error($"No subagent run matches epoch {epoch} in any subagent-runs.jsonl");

            Verdict parsedVerdict;
            try { parsedVerdict = Verdict.FromString(verdict); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            if (content?.Length > FieldLimits.EvalContentMax)
                return ToolResult.Error($"Eval content exceeds {FieldLimits.EvalContentMax} characters");

            var now = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            var matchedRunTs = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            var report = new EvalReport(TicketId.FromString(ticketId), runId, parsedVerdict, content ?? "", matchedRunTs, now);

            var evt = DomainEvent("EvalVerdictRecorded", "ticket", ticketId, causedBy, now,
                new { ticket_id = ticketId, run_id = runId, verdict = parsedVerdict.Value });
            _ctx.EventStore.Track(evt);

            await _ctx.EvalReportRepo.UpsertAsync(report, ct);

            var result = ToolResult.Ok(new EvalSetResponse(ticketId, runId, verdict), evt.Id);
            await _ctx.Idempotency.StoreAsync(idempotencyKey, result, ct);
            TicketLog.EvalSet(_ctx.Logger, ticketId, runId, verdict);
            return result;
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }
}
