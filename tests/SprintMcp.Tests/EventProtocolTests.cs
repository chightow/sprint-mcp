using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Moq;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Application.Services;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Infrastructure.Persistence.Repositories;
using SprintMcp.Server.Handlers;

namespace SprintMcp.Tests;

public class EventProtocolTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public EventProtocolTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public async Task InitializeAsync()
    {
        using var ctx = new AppDbContext(_options);
        await DatabaseInitializer.InitializeAsync(ctx);
    }

    public Task DisposeAsync()
    {
        _connection.Close();
        return Task.CompletedTask;
    }

    private AppDbContext CreateContext() => new(_options);

    private EventService CreateEventService(AppDbContext ctx)
    {
        var (eventStore, invariantEngine) = EventTestHelpers.CreateEventDeps(ctx);
        return new EventService(eventStore, invariantEngine, TimeProvider.System);
    }

    private async Task<(Sprint sprint, Ticket ticket)> SetupSprintWithTicketAsync(AppDbContext ctx, string phase = "planning")
    {
        var sprintRepo = new SprintRepository(ctx);
        var ticketRepo = new TicketRepository(ctx);
        var sprint = await sprintRepo.CreateNextAsync();
        while (phase != "planning" && sprint.Phase.Value != phase)
            sprint.AdvancePhase();
        if (phase != "planning")
            await sprintRepo.UpdateAsync(sprint);
        var ticket = await ticketRepo.CreateAsync("Test", "", Priority.Medium);
        ticket.AssignToSprint(sprint.Id);
        ticket.Touch(DateTime.UtcNow);
        await ticketRepo.UpdateAsync(ticket);
        return (sprint, ticket);
    }

    #region propose_event — acceptance

    [Fact]
    public async Task ProposeEvent_ValidAgentEvent_Accepted()
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "executing");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("FileWrite", "TKT-0001", """{"path":"src/main.cs"}""", ["ledger:ref1"]);

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.True(data!.Accepted);
        Assert.NotNull(data.EventId);
        Assert.True(data.Version > 0);
    }

    [Fact]
    public async Task ProposeEvent_FileReadInPlanning_Accepted()
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "planning");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("FileRead", "TKT-0001", """{"path":"src/spec.md"}""");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.True(data!.Accepted);
    }

    [Fact]
    public async Task ProposeEvent_FileWriteInExecuting_Accepted()
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "executing");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("FileWrite", "TKT-0001", """{"path":"src/main.cs"}""");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.True(data!.Accepted);
    }

    [Fact]
    public async Task ProposeEvent_ToolResultIsWriteFalseInPlanning_Accepted()
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "planning");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("ToolResult", "TKT-0001", """{"tool":"read_file","is_write":false}""");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.True(data!.Accepted);
    }

    #endregion

    #region propose_event — protocol types

    [Theory]
    [InlineData("ProtocolProposal")]
    [InlineData("ProtocolAccept")]
    [InlineData("ProtocolReject")]
    [InlineData("ProtocolAmendment")]
    [InlineData("ProtocolCancel")]
    public async Task ProposeEvent_ProtocolTypes_Accepted(string eventType)
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "planning");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent(eventType, "TKT-0001", """{"protocolId":"proto_abc"}""");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.True(data!.Accepted);
        Assert.NotNull(data.EventId);
    }

    [Theory]
    [InlineData("ProtocolProposal")]
    [InlineData("ProtocolAccept")]
    [InlineData("ProtocolReject")]
    [InlineData("ProtocolAmendment")]
    [InlineData("ProtocolCancel")]
    public async Task ProposeEvent_ProtocolTypes_InAnyPhase_Accepted(string eventType)
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "executing");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent(eventType, "TKT-0001", """{"protocolId":"proto_abc"}""");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.True(data!.Accepted);
        Assert.NotNull(data.EventId);
    }

    [Fact]
    public async Task ProposeEvent_ProtocolTypes_NotExecutionGated()
    {
        Assert.DoesNotContain("ProtocolProposal", EventTypeRegistry.ExecutionGatedTypes);
        Assert.DoesNotContain("ProtocolAccept", EventTypeRegistry.ExecutionGatedTypes);
        Assert.DoesNotContain("ProtocolReject", EventTypeRegistry.ExecutionGatedTypes);
        Assert.DoesNotContain("ProtocolAmendment", EventTypeRegistry.ExecutionGatedTypes);
        Assert.DoesNotContain("ProtocolCancel", EventTypeRegistry.ExecutionGatedTypes);
    }

    #endregion

    #region propose_event — rejections

    [Fact]
    public async Task ProposeEvent_DomainType_Rejected()
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "executing");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("TicketCreated", "TKT-0001", "{}");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.False(data!.Accepted);
        Assert.Null(data.EventId);
        Assert.Contains(data.RejectionReasons!, r => r.Contains("Domain event type"));
    }

    [Fact]
    public async Task ProposeEvent_UnknownType_Rejected()
    {
        var ctx = CreateContext();
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("BogusEvent", "TKT-0001", "{}");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.False(data!.Accepted);
        Assert.Contains(data.RejectionReasons!, r => r.Contains("Unknown event type"));
    }

    [Fact]
    public async Task ProposeEvent_FileWriteInPlanning_RejectedByPhaseGate()
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "planning");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("FileWrite", "TKT-0001", """{"path":"src/main.cs"}""");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.False(data!.Accepted);
        Assert.Contains(data.RejectionReasons!, r => r.Contains("PhaseGate"));
    }

    [Fact]
    public async Task ProposeEvent_ToolResultIsWriteTrueInPlanning_RejectedByPhaseGate()
    {
        var ctx = CreateContext();
        await SetupSprintWithTicketAsync(ctx, "planning");
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("ToolResult", "TKT-0001", """{"tool":"write_file","is_write":true}""");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.False(data!.Accepted);
        Assert.Contains(data.RejectionReasons!, r => r.Contains("PhaseGate"));
    }

    [Fact]
    public async Task ProposeEvent_InvalidJson_Rejected()
    {
        var ctx = CreateContext();
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("FileRead", "TKT-0001", "not json");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.False(data!.Accepted);
        Assert.Contains(data.RejectionReasons!, r => r.Contains("not valid JSON"));
    }

    [Fact]
    public async Task ProposeEvent_NoActiveSprint_RejectedByPhaseGate()
    {
        var ctx = CreateContext();
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ProposeEvent("FileWrite", "TKT-0001", """{"path":"src/main.cs"}""");

        Assert.False(result.IsError);
        var data = Deserialize<ProposeEventResponse>(result);
        Assert.False(data!.Accepted);
        Assert.Contains(data.RejectionReasons!, r => r.Contains("PhaseGate"));
    }

    #endregion

    #region list_events

    [Fact]
    public async Task ListEvents_ReturnsEventsAfterCursor()
    {
        var ctx = CreateContext();
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        await handler.ProposeEvent("FileRead", "TKT-0001", """{"path":"a"}""");
        await handler.ProposeEvent("FileRead", "TKT-0001", """{"path":"b"}""");

        var result = await handler.ListEvents(since: 0);

        Assert.False(result.IsError);
        var data = Deserialize<ListEventsResponse>(result);
        Assert.Equal(2, data!.Events.Count);
        Assert.True(data.NextCursor > 0);
        Assert.False(data.HasMore);
    }

    [Fact]
    public async Task ListEvents_HasMoreWhenExceedingTake()
    {
        var ctx = CreateContext();
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        for (var i = 0; i < 5; i++)
            await handler.ProposeEvent("FileRead", "TKT-0001", """{"path":"a"}""");

        var result = await handler.ListEvents(since: 0, take: 2);

        Assert.False(result.IsError);
        var data = Deserialize<ListEventsResponse>(result);
        Assert.Equal(2, data!.Events.Count);
        Assert.True(data.HasMore);
    }

    [Fact]
    public async Task ListEvents_FiltersByType()
    {
        var ctx = CreateContext();
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        await handler.ProposeEvent("FileRead", "TKT-0001", """{"path":"a"}""");
        await handler.ProposeEvent("GrepSearch", "TKT-0001", """{"pattern":"foo"}""");

        var result = await handler.ListEvents(since: 0, type: "GrepSearch");

        Assert.False(result.IsError);
        var data = Deserialize<ListEventsResponse>(result);
        Assert.Single(data!.Events);
        Assert.Equal("GrepSearch", data.Events[0].EventType);
    }

    [Fact]
    public async Task ListEvents_EmptyStream_ReturnsEmptyList()
    {
        var ctx = CreateContext();
        var svc = CreateEventService(ctx);
        var handler = new EventToolHandler(svc);

        var result = await handler.ListEvents(since: 0);

        Assert.False(result.IsError);
        var data = Deserialize<ListEventsResponse>(result);
        Assert.Empty(data!.Events);
        Assert.False(data.HasMore);
        Assert.Equal(0, data.NextCursor);
    }

    #endregion

    #region domain events — EventId on responses

    [Fact]
    public async Task CreateTicket_SetsEventIdOnResponse()
    {
        var ctx = CreateContext();
        var (eventStore, invariantEngine) = EventTestHelpers.CreateEventDeps(ctx);
        var sprintRepo = new SprintRepository(ctx);
        await sprintRepo.CreateNextAsync();

        var ticketSvc = new TicketService(new TicketServiceContext(
            new TicketRepository(ctx),
            new AcceptanceCriterionRepository(ctx),
            new DecisionRepository(ctx),
            new TestPlanItemRepository(ctx),
            new EvalReportRepository(ctx),
            sprintRepo,
            Mock.Of<ISubagentRunChecker>(),
            new IdempotencyService(new IdempotencyRepository(ctx, TimeProvider.System), TimeProvider.System),
            eventStore,
            invariantEngine,
            ".",
            TimeProvider.System,
            new TicketLock(),
            Mock.Of<ILogger<TicketService>>()));

        var result = await ticketSvc.CreateTicketAsync("Test Ticket", "", "medium");

        Assert.Equal("ok", result.Status);
        Assert.NotNull(result.EventId);
        Assert.True(result.EventId > 0);
    }

    [Fact]
    public async Task UpdateStatus_SetsEventIdOnResponse()
    {
        var ctx = CreateContext();
        var (sprint, ticket) = await SetupSprintWithTicketAsync(ctx, "executing");
        var (eventStore, invariantEngine) = EventTestHelpers.CreateEventDeps(ctx);

        var ticketSvc = new TicketService(new TicketServiceContext(
            new TicketRepository(ctx),
            new AcceptanceCriterionRepository(ctx),
            new DecisionRepository(ctx),
            new TestPlanItemRepository(ctx),
            new EvalReportRepository(ctx),
            new SprintRepository(ctx),
            Mock.Of<ISubagentRunChecker>(),
            new IdempotencyService(new IdempotencyRepository(ctx, TimeProvider.System), TimeProvider.System),
            eventStore,
            invariantEngine,
            ".",
            TimeProvider.System,
            new TicketLock(),
            Mock.Of<ILogger<TicketService>>()));

        var result = await ticketSvc.UpdateStatusAsync(ticket.Id, "in_progress");

        Assert.Equal("ok", result.Status);
        Assert.NotNull(result.EventId);
        Assert.True(result.EventId > 0);
    }

    [Fact]
    public async Task GetTicket_DoesNotSetEventId()
    {
        var ctx = CreateContext();
        var (sprint, ticket) = await SetupSprintWithTicketAsync(ctx, "planning");
        var (eventStore, invariantEngine) = EventTestHelpers.CreateEventDeps(ctx);

        var ticketSvc = new TicketService(new TicketServiceContext(
            new TicketRepository(ctx),
            new AcceptanceCriterionRepository(ctx),
            new DecisionRepository(ctx),
            new TestPlanItemRepository(ctx),
            new EvalReportRepository(ctx),
            new SprintRepository(ctx),
            Mock.Of<ISubagentRunChecker>(),
            new IdempotencyService(new IdempotencyRepository(ctx, TimeProvider.System), TimeProvider.System),
            eventStore,
            invariantEngine,
            ".",
            TimeProvider.System,
            new TicketLock(),
            Mock.Of<ILogger<TicketService>>()));

        var result = await ticketSvc.GetTicketAsync(ticket.Id);

        Assert.Equal("ok", result.Status);
        Assert.Null(result.EventId);
    }

    [Fact]
    public async Task CausedBy_PreservedInStoredEvent()
    {
        var ctx = CreateContext();
        var (sprint, ticket) = await SetupSprintWithTicketAsync(ctx, "executing");
        var (eventStore, invariantEngine) = EventTestHelpers.CreateEventDeps(ctx);

        var ticketSvc = new TicketService(new TicketServiceContext(
            new TicketRepository(ctx),
            new AcceptanceCriterionRepository(ctx),
            new DecisionRepository(ctx),
            new TestPlanItemRepository(ctx),
            new EvalReportRepository(ctx),
            new SprintRepository(ctx),
            Mock.Of<ISubagentRunChecker>(),
            new IdempotencyService(new IdempotencyRepository(ctx, TimeProvider.System), TimeProvider.System),
            eventStore,
            invariantEngine,
            ".",
            TimeProvider.System,
            new TicketLock(),
            Mock.Of<ILogger<TicketService>>()));

        var causedBy = new[] { "ledger:TKT-0001:abc123", "ledger:TKT-0001:def456" };
        await ticketSvc.UpdateStatusAsync(ticket.Id, "in_progress", causedBy);

        var events = await eventStore.GetSinceAsync(0);
        var statusEvent = events.FirstOrDefault(e => e.EventType == "TicketStatusChanged");
        Assert.NotNull(statusEvent);
        var storedCausedBy = statusEvent!.GetCausedBy();
        Assert.Equal(2, storedCausedBy.Length);
        Assert.Contains("ledger:TKT-0001:abc123", storedCausedBy);
        Assert.Contains("ledger:TKT-0001:def456", storedCausedBy);
    }

    #endregion

    private static T? Deserialize<T>(CallToolResult result)
    {
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        using var doc = JsonDocument.Parse(text.Text);
        return JsonSerializer.Deserialize<T>(doc.RootElement.GetProperty("data").GetRawText(), ToolResult.JsonOptions);
    }
}
