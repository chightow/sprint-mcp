using System.Text.Json;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Application.Services;

public class EventService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IEventStore _eventStore;
    private readonly InvariantEngine _invariantEngine;
    private readonly TimeProvider _timeProvider;
    private readonly IAgentContext _agentContext;

    public EventService(IEventStore eventStore, InvariantEngine invariantEngine, TimeProvider timeProvider, IAgentContext agentContext)
    {
        _eventStore = eventStore;
        _invariantEngine = invariantEngine;
        _timeProvider = timeProvider;
        _agentContext = agentContext;
    }

    public async Task<ToolResult> ProposeEventAsync(
        string eventType, string aggregateId, string eventDataJson,
        string[]? causedBy = null, CancellationToken ct = default)
    {
        if (!EventTypeRegistry.IsKnown(eventType))
            return ToolResult.Ok(new ProposeEventResponse(null, false, [$"Unknown event type: '{eventType}'"], 0));

        if (EventTypeRegistry.IsDomain(eventType))
            return ToolResult.Ok(new ProposeEventResponse(null, false, [$"Domain event type '{eventType}' cannot be proposed by agents"], 0));

        if (string.IsNullOrWhiteSpace(aggregateId))
            return ToolResult.Ok(new ProposeEventResponse(null, false, ["aggregate_id is required"], 0));

        try
        {
            using var doc = JsonDocument.Parse(eventDataJson);
        }
        catch
        {
            return ToolResult.Ok(new ProposeEventResponse(null, false, ["event_data is not valid JSON"], 0));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var evt = new Event(
            eventType, "agent", "agent_action", aggregateId,
            _agentContext.AgentId, causedBy ?? [], now, eventDataJson, null);

        var check = await _invariantEngine.CheckAsync(evt, ct);
        if (!check.Valid)
            return ToolResult.Ok(new ProposeEventResponse(null, false, [check.Failure!], 0));

        var saved = await _eventStore.AppendAsync(evt, ct);
        return ToolResult.Ok(new ProposeEventResponse(saved.Id, true, null, saved.Id));
    }

    public async Task<ToolResult> ListEventsAsync(
        long? since = null, string? type = null, string? aggregateId = null,
        int? take = null, CancellationToken ct = default)
    {
        var pageSize = take ?? 100;
        if (pageSize < 1) pageSize = 100;
        if (pageSize > 1000) pageSize = 1000;

        var events = await _eventStore.GetSinceAsync(since ?? 0, pageSize + 1, type, aggregateId, ct);
        var hasMore = events.Count > pageSize;
        var page = hasMore ? events.Take(pageSize).ToList() : events;
        var nextCursor = page.Count > 0 ? page.Max(e => e.Id) : since ?? 0;

        var dtos = page.Select(e => new EventDto(
            e.Id,
            e.EventType,
            e.Category,
            e.AggregateType,
            e.AggregateId,
            e.GetCausedBy(),
            e.Id,
            e.OccurredAt.ToString("O"),
            JsonDocument.Parse(e.EventData).RootElement.Clone()
        )).ToList();

        return ToolResult.Ok(new ListEventsResponse(dtos, hasMore, nextCursor));
    }
}
