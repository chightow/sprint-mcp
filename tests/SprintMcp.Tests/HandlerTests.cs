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
using SprintMcp.Domain.Repositories;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Infrastructure.Persistence.Repositories;
using SprintMcp.Server.Handlers;

namespace SprintMcp.Tests;

public class HandlerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public HandlerTests()
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

    [Fact]
    public async Task TicketHandler_UnknownAction_ReturnsError()
    {
        var ctx = CreateContext();
        var svc = CreateTicketService(ctx);
        var handler = new TicketHandler(svc);
        var result = await handler.HandleTicket("bogus");
        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Unknown action", text.Text);
    }

    [Fact]
    public async Task TicketHandler_CreateAction_WithoutSprint_ReturnsError()
    {
        var ctx = CreateContext();
        var svc = CreateTicketService(ctx);
        var handler = new TicketHandler(svc);
        var result = await handler.HandleTicket("create", title: "Test", description: "Desc", priority: "high");
        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("No active sprint", text.Text);
    }

    [Fact]
    public async Task TicketHandler_GetAction_NoTicket_ReturnsError()
    {
        var ctx = CreateContext();
        var svc = CreateTicketService(ctx);
        var handler = new TicketHandler(svc);
        var result = await handler.HandleTicket("get", ticket_id: "TKT-9999");
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task TicketHandler_Exception_ReturnsError()
    {
        var ctx = CreateContext();
        ctx.Dispose();
        var svc = CreateTicketService(ctx);
        var handler = new TicketHandler(svc);
        var result = await handler.HandleTicket("get", ticket_id: "TKT-0001");
        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Cannot access", text.Text);
    }

    [Fact]
    public async Task SprintHandler_UnknownAction_ReturnsError()
    {
        var ctx = CreateContext();
        var svc = CreateSprintService(ctx);
        var handler = new SprintHandler(svc);
        var result = await handler.HandleSprint("bogus");
        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Unknown action", text.Text);
    }

    [Fact]
    public async Task SprintHandler_StartAction_CreatesSprint()
    {
        var ctx = CreateContext();
        var svc = CreateSprintService(ctx);
        var handler = new SprintHandler(svc);
        var result = await handler.HandleSprint("start", title: "Test sprint", priority: "high");
        Assert.False(result.IsError);
        var data = Deserialize<SprintStartedResponse>(result);
        Assert.NotNull(data!.SprintId);
    }

    [Fact]
    public async Task SprintHandler_BoardAction_WithoutActive_ReturnsError()
    {
        var ctx = CreateContext();
        var svc = CreateSprintService(ctx);
        var handler = new SprintHandler(svc);
        var result = await handler.HandleSprint("board");
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task SprintHandler_Exception_ReturnsError()
    {
        var ctx = CreateContext();
        ctx.Dispose();
        var svc = CreateSprintService(ctx);
        var handler = new SprintHandler(svc);
        var result = await handler.HandleSprint("start");
        Assert.True(result.IsError);
    }

    [Fact]
    public void HandlerUtils_ToResult_Ok_SerializesCorrectly()
    {
        var toolResult = ToolResult.Ok(new TicketCreatedResponse("TKT-0001", "Test"));
        var callResult = HandlerUtils.ToResult(toolResult);
        Assert.False(callResult.IsError);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(callResult.Content));
        using var doc = JsonDocument.Parse(text.Text);
        var root = doc.RootElement;
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("TKT-0001", root.GetProperty("data").GetProperty("ticket_id").GetString());
        Assert.Equal("Test", root.GetProperty("data").GetProperty("title").GetString());
    }

    [Fact]
    public void HandlerUtils_ToResult_Error_SerializesCorrectly()
    {
        var toolResult = ToolResult.Error("Something went wrong");
        var callResult = HandlerUtils.ToResult(toolResult);
        Assert.True(callResult.IsError);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(callResult.Content));
        using var doc = JsonDocument.Parse(text.Text);
        Assert.Equal("error", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Something went wrong", doc.RootElement.GetProperty("message").GetString());
    }

    private TicketService CreateTicketService(AppDbContext ctx, ISubagentRunChecker? checker = null)
    {
        if (checker is null)
        {
            var mock = new Mock<ISubagentRunChecker>();
            mock.Setup(m => m.CheckRunAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            checker = mock.Object;
        }
        return new TicketService(new TicketServiceContext(
            new TicketRepository(ctx),
            new AcceptanceCriterionRepository(ctx),
            new DecisionRepository(ctx),
            new TestPlanItemRepository(ctx),
            new EvalReportRepository(ctx),
            new SprintRepository(ctx),
            checker,
            new IdempotencyService(new IdempotencyRepository(ctx, TimeProvider.System), TimeProvider.System),
            ".",
            TimeProvider.System,
            new TicketLock(),
            Mock.Of<ILogger<TicketService>>()));
    }

    private SprintService CreateSprintService(AppDbContext ctx, ISubagentRunChecker? checker = null)
    {
        if (checker is null)
        {
            var mock = new Mock<ISubagentRunChecker>();
            mock.Setup(m => m.CheckRunAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            checker = mock.Object;
        }
        return new SprintService(
            new TicketRepository(ctx),
            new SprintRepository(ctx),
            new SprintHandoffRepository(ctx),
            new ActiveTaskRepository(ctx),
            new EvalReportRepository(ctx),
            checker,
            new MockTransactionManager(),
            Mock.Of<ILogger<SprintService>>(),
            ".",
            TimeProvider.System,
            new SprintLock());
    }

    private static T? Deserialize<T>(CallToolResult result)
    {
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        using var doc = JsonDocument.Parse(text.Text);
        var data = doc.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<T>(data.GetRawText(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
    }
}
