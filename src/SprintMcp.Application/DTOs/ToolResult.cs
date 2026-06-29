namespace SprintMcp.Application.DTOs;

public sealed record ToolResult
{
    public string Status { get; init; } = "ok";
    public Dictionary<string, object> Data { get; init; } = [];
    public string? Message { get; init; }

    public static ToolResult Ok(Dictionary<string, object> data) => new() { Status = "ok", Data = data };
    public static ToolResult Error(string message) => new() { Status = "error", Message = message, Data = [] };
}
