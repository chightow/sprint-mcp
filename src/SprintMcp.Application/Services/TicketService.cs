using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Services;

public partial class TicketService
{
    private static readonly SemaphoreSlim _mutex = new(1, 1);
    private static readonly Regex RunIdPattern = RunIdRegex();

    [GeneratedRegex(@"^\d{1,10}-.+$")]
    private static partial Regex RunIdRegex();

    private readonly ITicketRepository _ticketRepo;
    private readonly IAcceptanceCriterionRepository _criterionRepo;
    private readonly IDecisionRepository _decisionRepo;
    private readonly ITestPlanItemRepository _testPlanRepo;
    private readonly IEvalReportRepository _evalReportRepo;
    private readonly ILogger<TicketService> _logger;

    public async Task<ToolResult> CreateTicketAsync(string title, string description, string priority, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ToolResult.Error("Title cannot be empty");

        Priority prio;
        try { prio = Priority.FromString(priority); }
        catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

        await _mutex.WaitAsync(ct);
        try
        {
            var ticket = await _ticketRepo.CreateAsync(title, description ?? "", prio, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticket.Id,
                ["title"] = ticket.Title
            });
        }
        finally
        {
            _mutex.Release();
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

    public TicketService(
        ITicketRepository ticketRepo,
        IAcceptanceCriterionRepository criterionRepo,
        IDecisionRepository decisionRepo,
        ITestPlanItemRepository testPlanRepo,
        IEvalReportRepository evalReportRepo,
        ILogger<TicketService> logger)
    {
        _ticketRepo = ticketRepo;
        _criterionRepo = criterionRepo;
        _decisionRepo = decisionRepo;
        _testPlanRepo = testPlanRepo;
        _evalReportRepo = evalReportRepo;
        _logger = logger;
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
        await _mutex.WaitAsync(ct);
        try
        {
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

            var invalid = (ticket.Status.Value, status.Value) switch
            {
                ("closed", not "archived") => "Cannot leave closed",
                ("cancelled", not "archived") => "Cannot leave cancelled",
                ("archived", _) => "Archived tickets are frozen",
                _ => null
            };
            if (invalid is not null)
                return ToolResult.Error(invalid);

            ticket.ChangeStatus(status);
            await _ticketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["new_status"] = status.Value
            });
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ToolResult> AddCriterionAsync(string ticketId, string criterionText, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (string.IsNullOrWhiteSpace(criterionText))
                return ToolResult.Error("Criterion text cannot be empty");

            var ordinal = await _criterionRepo.GetNextOrdinalAsync(ticketId, ct);
            var criterion = new AcceptanceCriterion
            {
                TicketId = ticketId,
                Ordinal = ordinal,
                Text = criterionText,
                Satisfied = false
            };
            await _criterionRepo.AddAsync(criterion, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["criterion"] = criterionText,
                ["ordinal"] = ordinal
            });
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ToolResult> CheckCriterionAsync(string ticketId, int? criterionId, int? ordinal, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
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

            target.Satisfied = !target.Satisfied;
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
            _mutex.Release();
        }
    }

    public async Task<ToolResult> SetPlanAsync(string ticketId, string? tier, string? approach, string? files, bool approve = false, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (tier is not null)
            {
                TicketTier parsedTier;
                try { parsedTier = TicketTier.FromString(tier); }
                catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }
                ticket.Tier = parsedTier;
            }

            if (approach is not null) ticket.PlanApproach = approach;
            if (files is not null) ticket.PlanFiles = files;
            if (approve) ticket.MarkPlanApproved();

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
            _mutex.Release();
        }
    }

    public async Task<ToolResult> AddDecisionAsync(string ticketId, string title, string rationale, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (string.IsNullOrWhiteSpace(title))
                return ToolResult.Error("Decision title cannot be empty");

            var decision = new Decision
            {
                TicketId = ticketId,
                Title = title,
                Rationale = rationale ?? ""
            };
            await _decisionRepo.AddAsync(decision, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["decision"] = title
            });
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ToolResult> AddTestAsync(string ticketId, string description, string expected, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (string.IsNullOrWhiteSpace(description))
                return ToolResult.Error("Test description cannot be empty");

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
            _mutex.Release();
        }
    }

    public async Task<ToolResult> UpdateTestAsync(string ticketId, int ordinal, string status, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            TestPlanStatus testStatus;
            try { testStatus = TestPlanStatus.FromString(status); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            var items = await _testPlanRepo.GetByTicketIdAsync(ticketId, ct);
            var item = items.FirstOrDefault(t => t.Ordinal == ordinal);
            if (item is null)
                return ToolResult.Error($"Test plan item {ordinal} not found for ticket {ticketId}");

            item.Status = testStatus;
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
            _mutex.Release();
        }
    }

    public async Task<ToolResult> SetSummaryAsync(string ticketId, string summary, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            ticket.Summary = summary ?? "";
            await _ticketRepo.UpdateAsync(ticket, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId
            });
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ToolResult> SetEvalAsync(string ticketId, string runId, string verdict, string content, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var ticket = await _ticketRepo.GetByIdAsync(ticketId, ct);
            if (ticket is null)
                return ToolResult.Error($"Ticket '{ticketId}' not found");

            if (string.IsNullOrWhiteSpace(runId) || !RunIdPattern.IsMatch(runId))
                return ToolResult.Error($"Invalid run ID '{runId}'. Expected format: <epoch>-<slug>");

            Verdict parsedVerdict;
            try { parsedVerdict = Verdict.FromString(verdict); }
            catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

            var report = new EvalReport
            {
                TicketId = ticketId,
                RunId = runId,
                Verdict = parsedVerdict,
                Content = content ?? ""
            };
            await _evalReportRepo.UpsertAsync(report, ct);

            return ToolResult.Ok(new Dictionary<string, object>
            {
                ["ticket_id"] = ticketId,
                ["run_id"] = runId,
                ["verdict"] = verdict
            });
        }
        finally
        {
            _mutex.Release();
        }
    }
}
