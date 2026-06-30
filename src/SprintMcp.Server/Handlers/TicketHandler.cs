using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SprintMcp.Application.Services;

namespace SprintMcp.Server.Handlers;

[McpServerToolType]
public class TicketHandler(TicketService ticketService)
{
    [McpServerTool(Name = "ticket"), Description(@"Manage tickets. Actions: create, get, list, status, add_criterion, check_criterion, set_plan, add_decision, add_test, update_test, set_summary, set_eval.")]
    public async Task<CallToolResult> HandleTicket(
        [Description("Action: create, get, list, status, add_criterion, check_criterion, set_plan, add_decision, add_test, update_test, set_summary, set_eval")] string action,
        [Description("The ticket ID")] string? ticket_id = null,
        [Description("Ticket title (for create)")] string? title = null,
        [Description("Ticket description")] string? description = null,
        [Description("Priority: low, medium, high, critical")] string? priority = null,
        [Description("New status: open, in_progress, closed, cancelled, archived")] string? new_status = null,
        [Description("Criterion text to add")] string? criterion = null,
        [Description("Criterion ID to toggle")] int? criterion_id = null,
        [Description("Criterion ordinal to toggle")] int? ordinal = null,
        [Description("Tier: trivial, regular, complex")] string? tier = null,
        [Description("Plan approach description")] string? approach = null,
        [Description("Plan files")] string? files = null,
        [Description("Approve the plan")] bool approve = false,
        [Description("Decision title")] string? decision_title = null,
        [Description("Decision rationale")] string? rationale = null,
        [Description("Test description")] string? test_description = null,
        [Description("Test expected result")] string? expected = null,
        [Description("Test status: pending, pass, fail, blocked")] string? test_status = null,
        [Description("Summary text")] string? summary = null,
        [Description("Eval run ID")] string? run_id = null,
        [Description("Eval verdict: pass, fail, pending")] string? verdict = null,
        [Description("Eval content")] string? content = null,
        [Description("Idempotency key to prevent duplicate operations")] string? idempotency_key = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = action switch
            {
                "create" => await ticketService.CreateTicketAsync(title ?? "", description ?? "", priority ?? "medium", idempotency_key, ct),
                "get" => await ticketService.GetTicketAsync(ticket_id ?? "", ct),
                "list" => await ticketService.ListTicketsAsync(ct),
                "status" => await ticketService.UpdateStatusAsync(ticket_id ?? "", new_status ?? "", ct),
                "add_criterion" => await ticketService.AddCriterionAsync(ticket_id ?? "", criterion ?? "", idempotency_key, ct),
                "check_criterion" => await ticketService.CheckCriterionAsync(ticket_id ?? "", criterion_id, ordinal, ct),
                "set_plan" => await ticketService.SetPlanAsync(ticket_id ?? "", tier, approach, files, approve, ct),
                "add_decision" => await ticketService.AddDecisionAsync(ticket_id ?? "", decision_title ?? "", rationale ?? "", idempotency_key, ct),
                "add_test" => await ticketService.AddTestAsync(ticket_id ?? "", test_description ?? "", expected ?? "", ct),
                "update_test" => ordinal is not null
                    ? await ticketService.UpdateTestAsync(ticket_id ?? "", ordinal.Value, test_status ?? "", ct)
                    : Application.DTOs.ToolResult.Error("ordinal is required for update_test"),
                "set_summary" => await ticketService.SetSummaryAsync(ticket_id ?? "", summary ?? "", ct),
                "set_eval" => await ticketService.SetEvalAsync(ticket_id ?? "", run_id ?? "", verdict ?? "", content ?? "", idempotency_key, ct),
                _ => Application.DTOs.ToolResult.Error($"Unknown action: {action}")
            };
            return HandlerUtils.ToResult(result);
        }
        catch (Exception ex)
        {
            return HandlerUtils.ToResult(Application.DTOs.ToolResult.Error(ex.Message));
        }
    }
}
