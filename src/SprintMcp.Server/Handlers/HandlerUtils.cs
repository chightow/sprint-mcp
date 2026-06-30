using System.Text.Json;
using ModelContextProtocol.Protocol;
using SprintMcp.Application.DTOs;

namespace SprintMcp.Server.Handlers;

public static class HandlerUtils
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static CallToolResult ToResult(ToolResult result)
    {
        var json = JsonSerializer.Serialize(result, JsonOptions);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }],
            IsError = result.Status == "error"
        };
    }
}
