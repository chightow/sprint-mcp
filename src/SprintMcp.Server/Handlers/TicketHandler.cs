using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SprintMcp.Application.Services;

namespace SprintMcp.Server.Handlers;

[McpServerToolType]
public class TicketHandler(TicketService ticketService)
{
    [McpServerTool(Name = "ticket_create"), Description("Create a new ticket in a sprint's planning phase.")]
    public async Task<CallToolResult> CreateTicket(
        [Description("Ticket title")] string title,
        [Description("Priority: low, medium, high, critical")] string? priority = null,
        [Description("Ticket description")] string? description = null,
        [Description("Idempotency key to prevent duplicate operations")] string? idempotency_key = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        [Description("Sprint ID. Uses active sprint if omitted.")] string? sprint_id = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.CreateTicketAsync(title, description ?? "", priority ?? "medium", idempotency_key, caused_by, sprint_id, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_get"), Description("Get detailed ticket info including criteria, decisions, test plan, and eval report.")]
    public async Task<CallToolResult> GetTicket(
        [Description("The ticket ID")] string ticket_id,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.GetTicketAsync(ticket_id, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_list"), Description("List all tickets with optional pagination.")]
    public async Task<CallToolResult> ListTickets(
        [Description("Pagination: skip N records")] int? skip = null,
        [Description("Pagination: take N records")] int? take = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.ListTicketsAsync(skip ?? 0, take ?? 100, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_status"), Description("Update ticket status (open, in_progress, closed, cancelled, archived). Requires executing phase.")]
    public async Task<CallToolResult> UpdateStatus(
        [Description("The ticket ID")] string ticket_id,
        [Description("New status: open, in_progress, closed, cancelled, archived")] string new_status,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.UpdateStatusAsync(ticket_id, new_status, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_add_criterion"), Description("Add an acceptance criterion to a ticket. Requires planning phase.")]
    public async Task<CallToolResult> AddCriterion(
        [Description("The ticket ID")] string ticket_id,
        [Description("Criterion text to add")] string criterion,
        [Description("Idempotency key to prevent duplicate operations")] string? idempotency_key = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.AddCriterionAsync(ticket_id, criterion, idempotency_key, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_check_criterion"), Description("Mark a criterion as satisfied or unsatisfied. Requires executing phase.")]
    public async Task<CallToolResult> CheckCriterion(
        [Description("The ticket ID")] string ticket_id,
        [Description("Criterion ID to toggle")] int? criterion_id = null,
        [Description("Criterion ordinal to toggle")] int? ordinal = null,
        [Description("Satisfied state")] bool? satisfied = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.CheckCriterionAsync(ticket_id, criterion_id, ordinal, satisfied ?? true, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_set_plan"), Description("Set or approve plan for a ticket (tier, approach, files). Requires planning phase.")]
    public async Task<CallToolResult> SetPlan(
        [Description("The ticket ID")] string ticket_id,
        [Description("Tier: trivial, regular, complex")] string? tier = null,
        [Description("Plan approach description")] string? approach = null,
        [Description("Plan files")] string? files = null,
        [Description("Approve the plan")] bool? approve = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.SetPlanAsync(ticket_id, tier, approach, files, approve ?? false, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_add_decision"), Description("Record a decision made during planning. Requires planning phase.")]
    public async Task<CallToolResult> AddDecision(
        [Description("The ticket ID")] string ticket_id,
        [Description("Decision title")] string decision_title,
        [Description("Decision rationale")] string? rationale = null,
        [Description("Idempotency key to prevent duplicate operations")] string? idempotency_key = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.AddDecisionAsync(ticket_id, decision_title, rationale ?? "", idempotency_key, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_add_test"), Description("Add a test plan item to a ticket. Requires planning phase.")]
    public async Task<CallToolResult> AddTest(
        [Description("The ticket ID")] string ticket_id,
        [Description("Test description")] string test_description,
        [Description("Test expected result")] string? expected = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.AddTestAsync(ticket_id, test_description, expected ?? "", caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_update_test"), Description("Update test plan item status. Requires executing phase.")]
    public async Task<CallToolResult> UpdateTest(
        [Description("The ticket ID")] string ticket_id,
        [Description("Test ordinal")] int ordinal,
        [Description("Test status: pending, pass, fail, blocked")] string test_status,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.UpdateTestAsync(ticket_id, ordinal, test_status, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_set_summary"), Description("Set the summary for a ticket. Requires evaluating phase.")]
    public async Task<CallToolResult> SetSummary(
        [Description("The ticket ID")] string ticket_id,
        [Description("Summary text")] string summary,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.SetSummaryAsync(ticket_id, summary, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "ticket_set_eval"), Description("Set evaluation report for a ticket. Requires evaluating phase.")]
    public async Task<CallToolResult> SetEval(
        [Description("The ticket ID")] string ticket_id,
        [Description("Eval run ID (format: <epoch>-<slug>)")] string run_id,
        [Description("Eval verdict: pass, fail, pending")] string verdict,
        [Description("Eval content")] string? content = null,
        [Description("Idempotency key to prevent duplicate operations")] string? idempotency_key = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await ticketService.SetEvalAsync(ticket_id, run_id, verdict, content ?? "", idempotency_key, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }
}
