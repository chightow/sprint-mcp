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

    [GeneratedRegex(@"^\d{1,10}-.+$")]
    private static partial Regex RunIdRegex();

    private readonly TicketServiceContext _ctx;

    public TicketService(TicketServiceContext ctx)
    {
        _ctx = ctx;
    }

    private async Task<(ToolResult? Error, Sprint? Sprint)> RequirePhaseAsync(SprintPhase requiredPhase, CancellationToken ct)
    {
        var active = await _ctx.SprintRepo.GetActiveAsync(ct);
        if (active is null)
            return (ToolResult.Error("No active sprint."), null);
        if (active.Phase != requiredPhase)
            return (ToolResult.Error($"Action requires phase '{requiredPhase}', but sprint is in phase '{active.Phase}'."), null);
        return (null, active);
    }

    public async Task<ToolResult> CreateTicketAsync(string title, string description, string priority, string? idempotencyKey = null, CancellationToken ct = default)
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

        var (phaseError, active) = await RequirePhaseAsync(SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        var sprintTickets = await _ctx.TicketRepo.GetBySprintIdAsync(active!.Id, ct);
        if (sprintTickets.Count >= MaxTicketsPerSprint)
            return ToolResult.Error($"Sprint '{active.Id}' already has {MaxTicketsPerSprint} tickets. Cannot create more.");

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var cached = await _ctx.Idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var ticket = await _ctx.TicketRepo.CreateAsync(title, description ?? "", prio, ct);

            var result = ToolResult.Ok(new TicketCreatedResponse(ticket.Id, ticket.Title));
            await _ctx.Idempotency.StoreAsync(idempotencyKey, result, ct);
            TicketLog.Created(_ctx.Logger, ticket.Id, ticket.Title);
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
                t.Id, t.Title, t.Status.Value, t.Priority.Value, t.Tier.Value, t.SprintId ?? ""
            )).ToList()
        ));
    }

    public async Task<ToolResult> GetTicketAsync(string ticketId, CancellationToken ct = default)
    {
        var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        var criteria = await _ctx.CriterionRepo.GetByTicketIdAsync(ticketId, ct);
        var decisions = await _ctx.DecisionRepo.GetByTicketIdAsync(ticketId, ct);
        var testPlan = await _ctx.TestPlanRepo.GetByTicketIdAsync(ticketId, ct);
        var evalReport = await _ctx.EvalReportRepo.GetByTicketIdAsync(ticketId, ct);

        return ToolResult.Ok(new TicketDetailResponse(
            ticket.Id, ticket.Title, ticket.Description,
            ticket.Status.Value, ticket.Priority.Value, ticket.Tier.Value,
            ticket.SprintId ?? "",
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

    public async Task<ToolResult> UpdateStatusAsync(string ticketId, string newStatus, CancellationToken ct = default)
    {
        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Executing, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            TicketStatus status;
            try { status = TicketStatus.FromString(newStatus); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            if (ticket.Status.Value == status.Value)
                return ToolResult.Ok(new TicketStatusResponse(ticketId, status.Value));

            if (!ticket.Status.CanTransitionTo(status))
                return ToolResult.Error($"Cannot transition from '{ticket.Status}' to '{status}'.");

            ticket.ChangeStatus(status);
            await _ctx.TicketRepo.UpdateAsync(ticket, ct);

            TicketLog.StatusChanged(_ctx.Logger, ticketId, status.Value);

            return ToolResult.Ok(new TicketStatusResponse(ticketId, status.Value));
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> AddCriterionAsync(string ticketId, string criterionText, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(criterionText))
            return ToolResult.Error("Criterion text cannot be empty");
        if (criterionText.Length > FieldLimits.CriterionTextMax)
            return ToolResult.Error($"Criterion text exceeds {FieldLimits.CriterionTextMax} characters");

        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var cached = await _ctx.Idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            var ordinal = await _ctx.CriterionRepo.GetNextOrdinalAsync(ticketId, ct);
            var criterion = new AcceptanceCriterion(ticketId, ordinal, criterionText);
            await _ctx.CriterionRepo.AddAsync(criterion, ct);

            var result = ToolResult.Ok(new CriterionAddedResponse(ticketId, criterionText, ordinal));
            await _ctx.Idempotency.StoreAsync(idempotencyKey, result, ct);
            return result;
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> CheckCriterionAsync(string ticketId, int? criterionId, int? ordinal, bool satisfied = true, CancellationToken ct = default)
    {
        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Executing, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            AcceptanceCriterion? target = null;
            if (criterionId.HasValue)
                target = await _ctx.CriterionRepo.GetByTicketIdAndIdAsync(ticketId, criterionId.Value, ct);
            else if (ordinal.HasValue)
                target = await _ctx.CriterionRepo.GetByTicketIdAndOrdinalAsync(ticketId, ordinal.Value, ct);

            if (target is null)
                return ToolResult.Error(!criterionId.HasValue && !ordinal.HasValue
                    ? "Provide criterion_id or ordinal"
                    : $"Criterion not found (id={criterionId}, ordinal={ordinal})");

            target.Satisfied = satisfied;
            await _ctx.CriterionRepo.UpdateAsync(target, ct);

            return ToolResult.Ok(new CriterionCheckedResponse(ticketId, target.Id, target.Ordinal, target.Satisfied));
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> SetPlanAsync(string ticketId, string? tier, string? approach, string? files, bool approve = false, CancellationToken ct = default)
    {
        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (tier is not null)
            {
                TicketTier parsedTier;
                try { parsedTier = TicketTier.FromString(tier); }
                catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }
                ticket.ChangeTier(parsedTier);
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
            if (approve) ticket.MarkPlanApproved(_ctx.TimeProvider.GetUtcNow().UtcDateTime);

            await _ctx.TicketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new PlanSetResponse(ticketId, ticket.Tier.Value, ticket.PlanApprovedAt.HasValue));
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> AddDecisionAsync(string ticketId, string title, string rationale, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ToolResult.Error("Decision title cannot be empty");
        if (title.Length > FieldLimits.DecisionTitleMax)
            return ToolResult.Error($"Decision title exceeds {FieldLimits.DecisionTitleMax} characters");
        if (rationale?.Length > FieldLimits.DecisionRationaleMax)
            return ToolResult.Error($"Decision rationale exceeds {FieldLimits.DecisionRationaleMax} characters");

        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var cached = await _ctx.Idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            var decision = new Decision(ticketId, title, rationale ?? "");
            await _ctx.DecisionRepo.AddAsync(decision, ct);

            var result = ToolResult.Ok(new DecisionAddedResponse(ticketId, title));
            await _ctx.Idempotency.StoreAsync(idempotencyKey, result, ct);
            return result;
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> AddTestAsync(string ticketId, string description, string expected, CancellationToken ct = default)
    {
        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Planning, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (string.IsNullOrWhiteSpace(description))
                return ToolResult.Error("Test description cannot be empty");
            if (description.Length > FieldLimits.TestDescriptionMax)
                return ToolResult.Error($"Test description exceeds {FieldLimits.TestDescriptionMax} characters");
            if (expected?.Length > FieldLimits.TestExpectedMax)
                return ToolResult.Error($"Test expected value exceeds {FieldLimits.TestExpectedMax} characters");

            var ordinal = await _ctx.TestPlanRepo.GetNextOrdinalAsync(ticketId, ct);
            var item = new TestPlanItem(ticketId, ordinal, description, expected ?? "");
            await _ctx.TestPlanRepo.AddAsync(item, ct);

            return ToolResult.Ok(new TestAddedResponse(ticketId, ordinal, description));
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> UpdateTestAsync(string ticketId, int ordinal, string status, CancellationToken ct = default)
    {
        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Executing, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            TestPlanStatus testStatus;
            try { testStatus = TestPlanStatus.FromString(status); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            var item = await _ctx.TestPlanRepo.GetByTicketIdAndOrdinalAsync(ticketId, ordinal, ct);
            if (item is null)
                return ToolResult.Error($"Test plan item {ordinal} not found for ticket {ticketId}");

            item.Status = testStatus;
            item.UpdatedAt = _ctx.TimeProvider.GetUtcNow().UtcDateTime;
            await _ctx.TestPlanRepo.UpdateAsync(item, ct);

            return ToolResult.Ok(new TestUpdatedResponse(ticketId, ordinal, item.Status.Value));
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> SetSummaryAsync(string ticketId, string summary, CancellationToken ct = default)
    {
        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Evaluating, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (summary?.Length > FieldLimits.SummaryMax)
                return ToolResult.Error($"Summary exceeds {FieldLimits.SummaryMax} characters");

            ticket.SetSummary(summary ?? "");
            await _ctx.TicketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new SummarySetResponse(ticketId));
        }
        finally
        {
            _ctx.TicketLock.Release();
        }
    }

    public async Task<ToolResult> SetEvalAsync(string ticketId, string runId, string verdict, string content, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(runId) || !RunIdPattern.IsMatch(runId))
            return ToolResult.Error($"Invalid run ID '{runId}'. Expected format: <epoch>-<slug>");

        var parts = runId.Split('-', 3);
        if (!long.TryParse(parts[0], out var epoch))
            return ToolResult.Error($"Invalid run ID '{runId}': epoch must be numeric");

        var (phaseError, _) = await RequirePhaseAsync(SprintPhase.Evaluating, ct);
        if (phaseError is not null) return phaseError;

        await _ctx.TicketLock.WaitAsync(ct);
        try
        {
            var cached = await _ctx.Idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var ticket = await _ctx.TicketRepo.GetByIdAsync(ticketId, ct);
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
            var report = new EvalReport(ticketId, runId, parsedVerdict, content ?? "", matchedRunTs, now);
            await _ctx.EvalReportRepo.UpsertAsync(report, ct);

            var result = ToolResult.Ok(new EvalSetResponse(ticketId, runId, verdict));
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
