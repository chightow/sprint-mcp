using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SprintMcp.Application.Services;

namespace SprintMcp.Server.Handlers;

[McpServerToolType]
public class SprintHandler(SprintService sprintService)
{
    [McpServerTool(Name = "sprint_start"), Description("Start a new sprint, optionally with a ticket or creating one from title.")]
    public async Task<CallToolResult> StartSprint(
        [Description("Title for new sprint ticket")] string? title = null,
        [Description("Existing ticket ID to resume")] string? ticket_id = null,
        [Description("Priority: low, medium, high, critical")] string? priority = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await sprintService.StartSprintAsync(title, ticket_id, priority ?? "medium", caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "sprint_board"), Description("View sprint board with tickets, handoff, and active tasks.")]
    public async Task<CallToolResult> GetBoard(
        [Description("Sprint ID. Uses active sprint if omitted.")] string? sprint_id = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await sprintService.GetBoardAsync(sprint_id, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "sprint_close"), Description("Close a sprint. Validates tickets and eval reports first.")]
    public async Task<CallToolResult> CloseSprint(
        [Description("Sprint ID. Uses active sprint if omitted.")] string? sprint_id = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await sprintService.CloseSprintAsync(caused_by, sprint_id, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "sprint_advance_phase"), Description("Advance sprint to next phase (planning -> executing -> evaluating).")]
    public async Task<CallToolResult> AdvancePhase(
        [Description("Sprint ID. Uses active sprint if omitted.")] string? sprint_id = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await sprintService.AdvancePhaseAsync(caused_by, sprint_id, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "sprint_update_handoff"), Description("Update the sprint handoff document with current focus, progress, discoveries, and next steps.")]
    public async Task<CallToolResult> UpdateHandoff(
        [Description("Current focus description")] string? current_focus = null,
        [Description("In progress items")] string? in_progress = null,
        [Description("Discoveries made")] string? discoveries = null,
        [Description("Next steps")] string? next_steps = null,
        [Description("Sprint ID. Uses active sprint if omitted.")] string? sprint_id = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await sprintService.UpdateHandoffAsync(current_focus, in_progress, discoveries, next_steps, caused_by, sprint_id, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "sprint_add_task"), Description("Add a task reference to a sprint.")]
    public async Task<CallToolResult> AddTask(
        [Description("Task reference (e.g. ticket ID)")] string task_ref,
        [Description("Sprint ID. Uses active sprint if omitted.")] string? sprint_id = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await sprintService.AddActiveTaskAsync(task_ref, caused_by, sprint_id, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "sprint_remove_task"), Description("Remove a task from a sprint by its task ID.")]
    public async Task<CallToolResult> RemoveTask(
        [Description("Task ID to remove")] int task_id,
        [Description("Sprint ID. Uses active sprint if omitted.")] string? sprint_id = null,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await sprintService.RemoveActiveTaskAsync(task_id, caused_by, sprint_id, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }
}
