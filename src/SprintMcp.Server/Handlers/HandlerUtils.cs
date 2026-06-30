using System.Text.Json;
using ModelContextProtocol.Protocol;
using SprintMcp.Application.DTOs;

namespace SprintMcp.Server.Handlers;

public static class HandlerUtils
{
    public static CallToolResult ToMcpResult(this ToolResult result)
    {
        var json = JsonSerializer.Serialize(result, ToolResult.JsonOptions);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }],
            IsError = result.Status == "error"
        };
    }

    public static CallToolResult ToResult(ToolResult result) => result.ToMcpResult();
}
