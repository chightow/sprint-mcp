using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Services;

public class TicketService
{
    private readonly ITicketRepository _ticketRepo;
    private readonly IAcceptanceCriterionRepository _criterionRepo;
    private readonly IDecisionRepository _decisionRepo;
    private readonly ITestPlanItemRepository _testPlanRepo;
    private readonly IEvalReportRepository _evalReportRepo;

    public TicketService(
        ITicketRepository ticketRepo,
        IAcceptanceCriterionRepository criterionRepo,
        IDecisionRepository decisionRepo,
        ITestPlanItemRepository testPlanRepo,
        IEvalReportRepository evalReportRepo)
    {
        _ticketRepo = ticketRepo;
        _criterionRepo = criterionRepo;
        _decisionRepo = decisionRepo;
        _testPlanRepo = testPlanRepo;
        _evalReportRepo = evalReportRepo;
    }

    public async Task<ToolResult> GetTicketAsync(string ticketId)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        var criteria = await _criterionRepo.GetByTicketIdAsync(ticketId);
        var decisions = await _decisionRepo.GetByTicketIdAsync(ticketId);
        var testPlan = await _testPlanRepo.GetByTicketIdAsync(ticketId);
        var evalReport = await _evalReportRepo.GetByTicketIdAsync(ticketId);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticket.Id,
            ["title"] = ticket.Title,
            ["description"] = ticket.Description,
            ["status"] = ticket.Status.Value,
            ["priority"] = ticket.Priority.Value,
            ["tier"] = ticket.Tier,
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
                ["status"] = t.Status
            }).ToList<object>(),
            ["eval_report"] = evalReport is not null ? new Dictionary<string, object>
            {
                ["run_id"] = evalReport.RunId,
                ["verdict"] = evalReport.Verdict,
                ["matched_run_ts"] = evalReport.MatchedRunTs ?? ""
            } : null!
        });
    }

    public async Task<ToolResult> UpdateStatusAsync(string ticketId, string newStatus)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        TicketStatus status;
        try { status = TicketStatus.FromString(newStatus); }
        catch (ArgumentException ex) { return ToolResult.Error(ex.Message); }

        ticket.ChangeStatus(status);
        await _ticketRepo.UpdateAsync(ticket);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId,
            ["new_status"] = status.Value
        });
    }

    public async Task<ToolResult> AddCriterionAsync(string ticketId, string criterionText)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        if (string.IsNullOrWhiteSpace(criterionText))
            return ToolResult.Error("Criterion text cannot be empty");

        var ordinal = await _criterionRepo.GetNextOrdinalAsync(ticketId);
        var criterion = new AcceptanceCriterion
        {
            TicketId = ticketId,
            Ordinal = ordinal,
            Text = criterionText,
            Satisfied = false
        };
        await _criterionRepo.AddAsync(criterion);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId,
            ["criterion"] = criterionText,
            ["ordinal"] = ordinal
        });
    }

    public async Task<ToolResult> CheckCriterionAsync(string ticketId, int? criterionId, int? ordinal)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        var criteria = await _criterionRepo.GetByTicketIdAsync(ticketId);
        AcceptanceCriterion? target = null;

        if (criterionId.HasValue)
            target = criteria.FirstOrDefault(c => c.Id == criterionId.Value);
        else if (ordinal.HasValue)
            target = criteria.FirstOrDefault(c => c.Ordinal == ordinal.Value);

        if (target is null)
            return ToolResult.Error($"Criterion not found (id={criterionId}, ordinal={ordinal})");

        target.Satisfied = !target.Satisfied;
        await _criterionRepo.UpdateAsync(target);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId,
            ["criterion_id"] = target.Id,
            ["ordinal"] = target.Ordinal,
            ["satisfied"] = target.Satisfied
        });
    }

    public async Task<ToolResult> SetPlanAsync(string ticketId, string? tier, string? approach, string? files, bool approve = false)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        var validTiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "trivial", "regular", "complex" };
        if (tier is not null)
        {
            if (!validTiers.Contains(tier))
                return ToolResult.Error($"Invalid tier '{tier}'. Must be trivial, regular, or complex");
            ticket.Tier = tier.ToLowerInvariant();
        }

        if (approach is not null) ticket.PlanApproach = approach;
        if (files is not null) ticket.PlanFiles = files;
        if (approve) ticket.MarkPlanApproved();

        await _ticketRepo.UpdateAsync(ticket);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId,
            ["tier"] = ticket.Tier,
            ["plan_approved"] = ticket.PlanApprovedAt.HasValue
        });
    }

    public async Task<ToolResult> AddDecisionAsync(string ticketId, string title, string rationale)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
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
        await _decisionRepo.AddAsync(decision);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId,
            ["decision"] = title
        });
    }

    public async Task<ToolResult> AddTestAsync(string ticketId, string description, string expected)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        if (string.IsNullOrWhiteSpace(description))
            return ToolResult.Error("Test description cannot be empty");

        var ordinal = await _testPlanRepo.GetNextOrdinalAsync(ticketId);
        var item = new TestPlanItem
        {
            TicketId = ticketId,
            Ordinal = ordinal,
            Description = description,
            Expected = expected ?? ""
        };
        await _testPlanRepo.AddAsync(item);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId,
            ["ordinal"] = ordinal,
            ["description"] = description
        });
    }

    public async Task<ToolResult> UpdateTestAsync(string ticketId, int ordinal, string status)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "pending", "pass", "fail", "blocked" };
        if (!validStatuses.Contains(status))
            return ToolResult.Error($"Invalid test status '{status}'");

        var items = await _testPlanRepo.GetByTicketIdAsync(ticketId);
        var item = items.FirstOrDefault(t => t.Ordinal == ordinal);
        if (item is null)
            return ToolResult.Error($"Test plan item {ordinal} not found for ticket {ticketId}");

        item.Status = status.ToLowerInvariant();
        await _testPlanRepo.UpdateAsync(item);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId,
            ["ordinal"] = ordinal,
            ["status"] = item.Status
        });
    }

    public async Task<ToolResult> SetSummaryAsync(string ticketId, string summary)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        ticket.Summary = summary ?? "";
        await _ticketRepo.UpdateAsync(ticket);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId
        });
    }

    public async Task<ToolResult> SetEvalAsync(string ticketId, string runId, string verdict, string content)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket is null)
            return ToolResult.Error($"Ticket '{ticketId}' not found");

        var validVerdicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "pass", "fail", "pending" };
        if (!validVerdicts.Contains(verdict))
            return ToolResult.Error($"Invalid verdict '{verdict}'");

        var report = new EvalReport
        {
            TicketId = ticketId,
            RunId = runId,
            Verdict = verdict.ToLowerInvariant(),
            Content = content ?? ""
        };
        await _evalReportRepo.UpsertAsync(report);

        return ToolResult.Ok(new Dictionary<string, object>
        {
            ["ticket_id"] = ticketId,
            ["run_id"] = runId,
            ["verdict"] = verdict
        });
    }
}
