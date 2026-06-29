using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SprintMcp.Application.Services;

namespace SprintMcp.Server.Handlers;

[McpServerToolType]
public class SprintHandler(SprintService sprintService)
{
    [McpServerTool(Name = "sprint"), Description(@"Manage sprints. Actions: start, board, close.")]
    public async Task<CallToolResult> HandleSprint(
        [Description("Action: start, board, close")] string action,
        [Description("Title for new sprint ticket")] string? title = null,
        [Description("Existing ticket ID to resume")] string? ticket_id = null,
        [Description("Priority: low, medium, high, critical")] string? priority = null)
    {
        try
        {
            var result = action switch
            {
                "start" => await sprintService.StartSprintAsync(title, ticket_id, priority ?? "medium"),
                "board" => await sprintService.GetBoardAsync(),
                "close" => await sprintService.CloseSprintAsync(),
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
