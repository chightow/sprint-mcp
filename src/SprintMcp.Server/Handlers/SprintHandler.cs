using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SprintMcp.Application.Services;

namespace SprintMcp.Server.Handlers;

[McpServerToolType]
public class SprintHandler(SprintService sprintService)
{
    [McpServerTool(Name = "sprint"), Description(@"Manage sprints. Actions: start, board, close, update_handoff, add_task, remove_task.")]
    public async Task<CallToolResult> HandleSprint(
        [Description("Action: start, board, close, update_handoff, add_task, remove_task")] string action,
        [Description("Title for new sprint ticket")] string? title = null,
        [Description("Existing ticket ID to resume")] string? ticket_id = null,
        [Description("Priority: low, medium, high, critical")] string? priority = null,
        [Description("Current focus (for update_handoff)")] string? current_focus = null,
        [Description("In progress items (for update_handoff)")] string? in_progress = null,
        [Description("Discoveries (for update_handoff)")] string? discoveries = null,
        [Description("Next steps (for update_handoff)")] string? next_steps = null,
        [Description("Task reference (for add_task)")] string? task_ref = null,
        [Description("Task ID (for remove_task)")] int? task_id = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = action switch
            {
                "start" => await sprintService.StartSprintAsync(title, ticket_id, priority ?? "medium", ct),
                "board" => await sprintService.GetBoardAsync(ct),
                "close" => await sprintService.CloseSprintAsync(ct),
                "update_handoff" => await sprintService.UpdateHandoffAsync(current_focus, in_progress, discoveries, next_steps, ct),
                "add_task" => await sprintService.AddActiveTaskAsync(task_ref ?? "", ct),
                "remove_task" => await sprintService.RemoveActiveTaskAsync(task_id ?? 0, ct),
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
