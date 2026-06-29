using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SprintMcp.Application.Services;

namespace SprintMcp.Server.Handlers;

[McpServerToolType]
public class TicketHandler(TicketService ticketService)
{
    [McpServerTool(Name = "ticket"), Description(@"Manage tickets. Actions: get, status, add_criterion, check_criterion, set_plan, add_decision, add_test, update_test, set_summary, set_eval.")]
    public async Task<CallToolResult> HandleTicket(
        [Description("Action: get, status, add_criterion, check_criterion, set_plan, add_decision, add_test, update_test, set_summary, set_eval")] string action,
        [Description("The ticket ID")] string ticket_id,
        [Description("New status: open, in_progress, closed, cancelled, archived")] string? new_status = null,
        [Description("Criterion text to add")] string? criterion = null,
        [Description("Criterion ID to toggle")] int? criterion_id = null,
        [Description("Criterion ordinal to toggle")] int? ordinal = null,
        [Description("Tier: trivial, regular, complex")] string? tier = null,
        [Description("Plan approach description")] string? approach = null,
        [Description("Plan files")] string? files = null,
        [Description("Approve the plan")] bool approve = false,
        [Description("Decision title")] string? title = null,
        [Description("Decision rationale")] string? rationale = null,
        [Description("Test description")] string? description = null,
        [Description("Test expected result")] string? expected = null,
        [Description("Test status: pending, pass, fail, blocked")] string? test_status = null,
        [Description("Summary text")] string? summary = null,
        [Description("Eval run ID")] string? run_id = null,
        [Description("Eval verdict: pass, fail, pending")] string? verdict = null,
        [Description("Eval content")] string? content = null)
    {
        try
        {
            var result = action switch
            {
                "get" => await ticketService.GetTicketAsync(ticket_id),
                "status" => await ticketService.UpdateStatusAsync(ticket_id, new_status ?? ""),
                "add_criterion" => await ticketService.AddCriterionAsync(ticket_id, criterion ?? ""),
                "check_criterion" => await ticketService.CheckCriterionAsync(ticket_id, criterion_id, ordinal),
                "set_plan" => await ticketService.SetPlanAsync(ticket_id, tier, approach, files, approve),
                "add_decision" => await ticketService.AddDecisionAsync(ticket_id, title ?? "", rationale ?? ""),
                "add_test" => await ticketService.AddTestAsync(ticket_id, description ?? "", expected ?? ""),
                "update_test" => await ticketService.UpdateTestAsync(ticket_id, ordinal ?? 0, test_status ?? ""),
                "set_summary" => await ticketService.SetSummaryAsync(ticket_id, summary ?? ""),
                "set_eval" => await ticketService.SetEvalAsync(ticket_id, run_id ?? "", verdict ?? "", content ?? ""),
                _ => Application.DTOs.ToolResult.Error($"Unknown action: {action}")
            };
            return ToResult(result);
        }
        catch (Exception ex)
        {
            return ToResult(Application.DTOs.ToolResult.Error(ex.Message));
        }
    }

    private static CallToolResult ToResult(Application.DTOs.ToolResult result)
    {
        var json = JsonSerializer.Serialize(result);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }]
        };
    }
}
