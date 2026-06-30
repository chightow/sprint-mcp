using System.Text.Json;
using System.Text.Json.Serialization;

namespace SprintMcp.Application.DTOs;

public sealed record ToolResult
{
    public string Status { get; init; } = "ok";
    public object? Data { get; init; }
    public string? Message { get; init; }
    public long? EventId { get; init; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ToolResult Ok(object? data = null, long? eventId = null) => new() { Status = "ok", Data = data, EventId = eventId };
    public static ToolResult Error(string message) => new() { Status = "error", Message = message };

    public T? GetData<T>() where T : class
    {
        if (Data is T typed) return typed;
        if (Data is JsonElement json)
        {
            try { return JsonSerializer.Deserialize<T>(json.GetRawText(), JsonOptions); }
            catch { return null; }
        }
        return null;
    }
}
