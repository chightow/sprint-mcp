using System.Text.Json;

namespace SprintMcp.Application.DTOs;

public sealed record ToolResult
{
    public string Status { get; init; } = "ok";
    public object? Data { get; init; }
    public string? Message { get; init; }

    public static ToolResult Ok(object? data = null) => new() { Status = "ok", Data = data };
    public static ToolResult Error(string message) => new() { Status = "error", Message = message };

    public T? GetData<T>() where T : class
    {
        if (Data is T typed) return typed;
        if (Data is JsonElement json)
        {
            try { return JsonSerializer.Deserialize<T>(json.GetRawText()); }
            catch { return null; }
        }
        return null;
    }
}
