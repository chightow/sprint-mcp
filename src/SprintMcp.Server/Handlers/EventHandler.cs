using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SprintMcp.Application.Services;

namespace SprintMcp.Server.Handlers;

[McpServerToolType]
public class EventToolHandler(EventService eventService)
{
    [McpServerTool(Name = "propose_event"), Description("Propose an agent-action event. Rejected if type is domain-category, unknown, or invariants fail.")]
    public async Task<CallToolResult> ProposeEvent(
        [Description("Event type: FileRead, FileWrite, EditString, RunTerminal, TerminalOutput, GrepSearch, FileSearch, ToolResult, Decision, TaskComplete, AskQuestions, FetchWebpage")] string event_type,
        [Description("Aggregate ID: the ticket ID being worked on, or '<sprint_id>:system' for non-ticket actions")] string aggregate_id,
        [Description("Event payload as JSON string")] string event_data,
        [Description("Causal references to sprint-do ledger entries. Opaque strings, sprint-mcp stores without validation.")] string[]? caused_by = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await eventService.ProposeEventAsync(event_type, aggregate_id, event_data, caused_by, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }

    [McpServerTool(Name = "list_events"), Description("List events since a version cursor. Returns all events with version > since.")]
    public async Task<CallToolResult> ListEvents(
        [Description("Version cursor. Returns events with version > this value. Omit to get all events.")] long? since = null,
        [Description("Filter by event type")] string? type = null,
        [Description("Filter by aggregate ID")] string? aggregate_id = null,
        [Description("Max events to return (default 100, max 1000)")] int? take = null,
        CancellationToken ct = default)
    {
        try
        {
            var result = await eventService.ListEventsAsync(since, type, aggregate_id, take, ct);
            return result.ToMcpResult();
        }
        catch (Exception ex)
        {
            return Application.DTOs.ToolResult.Error(ex.Message).ToMcpResult();
        }
    }
}
