using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace SprintMcp.Server.Handlers;

public static class HandlerUtils
{
    public static CallToolResult ToResult(Application.DTOs.ToolResult result)
    {
        var json = JsonSerializer.Serialize(result);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }]
        };
    }
}
