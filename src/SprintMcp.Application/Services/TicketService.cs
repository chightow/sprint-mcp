using System.Text.RegularExpressions;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Services;

public partial class TicketService
{
    private static readonly Regex RunIdPattern = RunIdRegex();

    [GeneratedRegex(@"^\d{1,10}-.+$")]
    private static partial Regex RunIdRegex();

    private readonly ITicketRepository _ticketRepo;
    private readonly IAcceptanceCriterionRepository _criterionRepo;
    private readonly IDecisionRepository _decisionRepo;
    private readonly ITestPlanItemRepository _testPlanRepo;
    private readonly IEvalReportRepository _evalReportRepo;
    private readonly ISprintRepository _sprintRepo;
    private readonly ISubagentRunChecker _runChecker;
    private readonly IdempotencyService _idempotency;
    private readonly string _projectRoot;
    private readonly TimeProvider _timeProvider;
    private readonly ITicketLock _ticketLock;

    public TicketService(
        ITicketRepository ticketRepo,
        IAcceptanceCriterionRepository criterionRepo,
        IDecisionRepository decisionRepo,
        ITestPlanItemRepository testPlanRepo,
        IEvalReportRepository evalReportRepo,
        ISprintRepository sprintRepo,
        ISubagentRunChecker runChecker,
        IdempotencyService idempotency,
        string projectRoot,
        TimeProvider timeProvider,
        ITicketLock ticketLock)
    {
        _ticketRepo = ticketRepo;
        _criterionRepo = criterionRepo;
        _decisionRepo = decisionRepo;
        _testPlanRepo = testPlanRepo;
        _evalReportRepo = evalReportRepo;
        _sprintRepo = sprintRepo;
        _runChecker = runChecker;
        _idempotency = idempotency;
        _projectRoot = projectRoot;
        _timeProvider = timeProvider;
        _ticketLock = ticketLock;
    }

    private async Task<ToolResult?> RequirePhaseAsync(string requiredPhase, CancellationToken ct)
    {
        var active = await _sprintRepo.GetActiveAsync(ct);
        if (active is null)
            return ToolResult.Error("No active sprint.");
        if (active.Phase.Value != requiredPhase)
            return ToolResult.Error($"Action requires phase '{requiredPhase}', but sprint is in phase '{active.Phase}'.");
        return null;
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

        await _ticketLock.WaitAsync(ct);
        try
        {
            var cached = await _idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var active = await _sprintRepo.GetActiveAsync(ct);
            if (active is null)
                return ToolResult.Error("No active sprint.");
            if (active.Phase.Value != "planning")
                return ToolResult.Error($"Action requires phase 'planning', but sprint is in phase '{active.Phase}'.");

            var sprintTickets = await _ticketRepo.GetBySprintIdAsync(active.Id, ct);
            if (sprintTickets.Count >= 50)
                return ToolResult.Error($"Sprint '{active.Id}' already has 50 tickets. Cannot create more.");

            var ticket = await _ticketRepo.CreateAsync(title, description ?? "", prio, ct);

            var result = ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticket.Id,
                ["title"] = ticket.Title
            });
            await _idempotency.StoreAsync(idempotencyKey, result, ct);
            return result;
        }
        finally
        {
            _ticketLock.Release();
        }
    }

    public async Task<ToolResult> ListTicketsAsync(CancellationToken ct = default)
    {
        var tickets = await _ticketRepo.GetAllAsync(ct);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["tickets"] = tickets.Select(t => new Dictionary<string, object>
            {
                ["id"] = t.Id,
                ["title"] = t.Title,
                ["status"] = t.Status.Value,
                ["priority"] = t.Priority.Value,
                ["tier"] = t.Tier.Value,
                ["sprint_id"] = t.SprintId ?? ""
            }).ToList<object>()
        });
    }

    public async Task<ToolResult> GetTicketAsync(string ticketId, CancellationToken ct = default)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        var criteria = await _criterionRepo.GetByTicketIdAsync(ticketId, ct);
        var decisions = await _decisionRepo.GetByTicketIdAsync(ticketId, ct);
        var testPlan = await _testPlanRepo.GetByTicketIdAsync(ticketId, ct);
        var evalReport = await _evalReportRepo.GetByTicketIdAsync(ticketId, ct);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticket.Id,
            ["title"] = ticket.Title,
            ["description"] = ticket.Description,
            ["status"] = ticket.Status.Value,
            ["priority"] = ticket.Priority.Value,
            ["tier"] = ticket.Tier.Value,
            ["sprint_id"] = ticket.SprintId ?? "",
            ["plan_approach"] = ticket.PlanApproach,
            ["plan_files"] = ticket.PlanFiles,
            ["plan_approved_at"] = ticket.PlanApprovedAt?.ToString("O") ?? "",
            ["summary"] = ticket.Summary,
            ["created_at"] = ticket.CreatedAt.ToString("O"),
            ["updated_at"] = ticket.UpdatedAt.ToString("O"),
            ["acceptance"] = criteria.Select(c => new Dictionary<string, object>
            {
                ["id"] = c.Id,
                ["ordinal"] = c.Ordinal,
                ["text"] = c.Text,
                ["satisfied"] = c.Satisfied
            }).ToList<object>(),
            ["decisions"] = decisions.Select(d => new Dictionary<string, object>
            {
                ["id"] = d.Id,
                ["title"] = d.Title,
                ["rationale"] = d.Rationale,
                ["created_at"] = d.CreatedAt.ToString("O")
            }).ToList<object>(),
            ["test_plan"] = testPlan.Select(t => new Dictionary<string, object>
            {
                ["id"] = t.Id,
                ["ordinal"] = t.Ordinal,
                ["description"] = t.Description,
                ["expected"] = t.Expected,
                ["status"] = t.Status.Value
            }).ToList<object>(),
            ["eval_report"] = evalReport is not null ? new Dictionary<string, object>
            {
                ["run_id"] = evalReport.RunId,
                ["verdict"] = evalReport.Verdict.Value,
                ["matched_run_ts"] = evalReport.MatchedRunTs ?? ""
            } : null!
        });
    }

    public async Task<ToolResult> UpdateStatusAsync(string ticketId, string newStatus, CancellationToken ct = default)
    {
        await _ticketLock.WaitAsync(ct);
        try
        {
            var phaseError = await RequirePhaseAsync("executing", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            TicketStatus status;
            try { status = TicketStatus.FromString(newStatus); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            if (ticket.Status.Value == status.Value)
                return ToolResult.Ok(new Dictionary<string, object>
                {
                    ["ticket_id"] = ticketId,
                    ["new_status"] = status.Value
                });

            if (!ticket.Status.CanTransitionTo(status))
                return ToolResult.Error($"Cannot transition from '{ticket.Status}' to '{status}'.");

            ticket.ChangeStatus(status);
            ticket.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _ticketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["new_status"] = status.Value
            });
        }
        finally
        {
            _ticketLock.Release();
        }
    }

    public async Task<ToolResult> AddCriterionAsync(string ticketId, string criterionText, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(criterionText))
            return ToolResult.Error("Criterion text cannot be empty");
        if (criterionText.Length > FieldLimits.CriterionTextMax)
            return ToolResult.Error($"Criterion text exceeds {FieldLimits.CriterionTextMax} characters");

        await _ticketLock.WaitAsync(ct);
        try
        {
            var cached = await _idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var phaseError = await RequirePhaseAsync("planning", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            var ordinal = await _criterionRepo.GetNextOrdinalAsync(ticketId, ct);
            var criterion = new AcceptanceCriterion
            {
                TicketId = ticketId,
                Ordinal = ordinal,
                Text = criterionText,
                Satisfied = false
            };
            await _criterionRepo.AddAsync(criterion, ct);

            var result = ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["criterion"] = criterionText,
                ["ordinal"] = ordinal
            });
            await _idempotency.StoreAsync(idempotencyKey, result, ct);
            return result;
        }
        finally
        {
            _ticketLock.Release();
        }
    }

    public async Task<ToolResult> CheckCriterionAsync(string ticketId, int? criterionId, int? ordinal, bool satisfied = true, CancellationToken ct = default)
    {
        await _ticketLock.WaitAsync(ct);
        try
        {
            var phaseError = await RequirePhaseAsync("executing", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            AcceptanceCriterion? target = null;
            if (criterionId.HasValue)
                target = await _criterionRepo.GetByTicketIdAndIdAsync(ticketId, criterionId.Value, ct);
            else if (ordinal.HasValue)
                target = await _criterionRepo.GetByTicketIdAndOrdinalAsync(ticketId, ordinal.Value, ct);

            if (target is null)
                return ToolResult.Error(!criterionId.HasValue && !ordinal.HasValue
                    ? "Provide criterion_id or ordinal"
                    : $"Criterion not found (id={criterionId}, ordinal={ordinal})");

            target.Satisfied = satisfied;
            await _criterionRepo.UpdateAsync(target, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["criterion_id"] = target.Id,
                ["ordinal"] = target.Ordinal,
                ["satisfied"] = target.Satisfied
            });
        }
        finally
        {
            _ticketLock.Release();
        }
    }

    public async Task<ToolResult> SetPlanAsync(string ticketId, string? tier, string? approach, string? files, bool approve = false, CancellationToken ct = default)
    {
        await _ticketLock.WaitAsync(ct);
        try
        {
            var phaseError = await RequirePhaseAsync("planning", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
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
                ticket.PlanApproach = approach;
            }
            if (files is not null)
            {
                if (files.Length > FieldLimits.PlanFilesMax)
                    return ToolResult.Error($"Plan files exceeds {FieldLimits.PlanFilesMax} characters");
                ticket.PlanFiles = files;
            }
            if (approve) ticket.MarkPlanApproved(_timeProvider.GetUtcNow().UtcDateTime);

            ticket.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _ticketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["tier"] = ticket.Tier.Value,
                ["plan_approved"] = ticket.PlanApprovedAt.HasValue
            });
        }
        finally
        {
            _ticketLock.Release();
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

        await _ticketLock.WaitAsync(ct);
        try
        {
            var cached = await _idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var phaseError = await RequirePhaseAsync("planning", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            var decision = new Decision
            {
                TicketId = ticketId,
                Title = title,
                Rationale = rationale ?? ""
            };
            await _decisionRepo.AddAsync(decision, ct);

            var result = ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["decision"] = title
            });
            await _idempotency.StoreAsync(idempotencyKey, result, ct);
            return result;
        }
        finally
        {
            _ticketLock.Release();
        }
    }

    public async Task<ToolResult> AddTestAsync(string ticketId, string description, string expected, CancellationToken ct = default)
    {
        await _ticketLock.WaitAsync(ct);
        try
        {
            var phaseError = await RequirePhaseAsync("planning", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (string.IsNullOrWhiteSpace(description))
                return ToolResult.Error("Test description cannot be empty");
            if (description.Length > FieldLimits.TestDescriptionMax)
                return ToolResult.Error($"Test description exceeds {FieldLimits.TestDescriptionMax} characters");
            if (expected?.Length > FieldLimits.TestExpectedMax)
                return ToolResult.Error($"Test expected value exceeds {FieldLimits.TestExpectedMax} characters");

            var ordinal = await _testPlanRepo.GetNextOrdinalAsync(ticketId, ct);
            var item = new TestPlanItem
            {
                TicketId = ticketId,
                Ordinal = ordinal,
                Description = description,
                Expected = expected ?? ""
            };
            await _testPlanRepo.AddAsync(item, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["ordinal"] = ordinal,
                ["description"] = description
            });
        }
        finally
        {
            _ticketLock.Release();
        }
    }

    public async Task<ToolResult> UpdateTestAsync(string ticketId, int ordinal, string status, CancellationToken ct = default)
    {
        await _ticketLock.WaitAsync(ct);
        try
        {
            var phaseError = await RequirePhaseAsync("executing", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            TestPlanStatus testStatus;
            try { testStatus = TestPlanStatus.FromString(status); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            var item = await _testPlanRepo.GetByTicketIdAndOrdinalAsync(ticketId, ordinal, ct);
            if (item is null)
                return ToolResult.Error($"Test plan item {ordinal} not found for ticket {ticketId}");

            item.Status = testStatus;
            item.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _testPlanRepo.UpdateAsync(item, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["ordinal"] = ordinal,
                ["status"] = item.Status.Value
            });
        }
        finally
        {
            _ticketLock.Release();
        }
    }

    public async Task<ToolResult> SetSummaryAsync(string ticketId, string summary, CancellationToken ct = default)
    {
        await _ticketLock.WaitAsync(ct);
        try
        {
            var phaseError = await RequirePhaseAsync("evaluating", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (summary?.Length > FieldLimits.SummaryMax)
                return ToolResult.Error($"Summary exceeds {FieldLimits.SummaryMax} characters");

            ticket.Summary = summary ?? "";
            ticket.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _ticketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId
            });
        }
        finally
        {
            _ticketLock.Release();
        }
    }

    public async Task<ToolResult> SetEvalAsync(string ticketId, string runId, string verdict, string content, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(runId) || !RunIdPattern.IsMatch(runId))
            return ToolResult.Error($"Invalid run ID '{runId}'. Expected format: <epoch>-<slug>");

        var parts = runId.Split('-', 3);
        if (!long.TryParse(parts[0], out var epoch))
            return ToolResult.Error($"Invalid run ID '{runId}': epoch must be numeric");

        await _ticketLock.WaitAsync(ct);
        try
        {
            var cached = await _idempotency.CheckAsync(idempotencyKey, ct);
            if (cached is not null) return cached;

            var phaseError = await RequirePhaseAsync("evaluating", ct);
            if (phaseError is not null) return phaseError;

            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (!await _runChecker.CheckRunAsync(epoch, _projectRoot, ct))
                return ToolResult.Error($"No subagent run matches epoch {epoch} in any subagent-runs.jsonl");

            Verdict parsedVerdict;
            try { parsedVerdict = Verdict.FromString(verdict); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            if (content?.Length > FieldLimits.EvalContentMax)
                return ToolResult.Error($"Eval content exceeds {FieldLimits.EvalContentMax} characters");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var report = new EvalReport
            {
                TicketId = ticketId,
                RunId = runId,
                Verdict = parsedVerdict,
                Content = content ?? "",
                UpdatedAt = now
            };
            await _evalReportRepo.UpsertAsync(report, ct);

            var result = ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["run_id"] = runId,
                ["verdict"] = verdict
            });
            await _idempotency.StoreAsync(idempotencyKey, result, ct);
            return result;
        }
        finally
        {
            _ticketLock.Release();
        }
    }
}
