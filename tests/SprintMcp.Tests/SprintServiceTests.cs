using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.Services;
using SprintMcp.Domain.Entities;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Infrastructure.Persistence.Repositories;

namespace SprintMcp.Tests;

public class SprintServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public SprintServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new AppDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Close();

    private AppDbContext CreateContext() => new(_options);

    private SprintService CreateService(AppDbContext ctx, ISubagentRunChecker? checker = null)
    {
        checker ??= Mock.Of<ISubagentRunChecker>(m => m.CheckRun(It.IsAny<long>(), It.IsAny<string>()) == true);
        return new SprintService(
            new TicketRepository(ctx),
            new SprintRepository(ctx),
            new SprintHandoffRepository(ctx),
            new ActiveTaskRepository(ctx),
            new EvalReportRepository(ctx),
            checker,
            ".");
    }

    [Fact]
    public async Task StartSprint_CreatesSprintAndTicket()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.StartSprintAsync("My ticket", null, "high");
        Assert.Equal("ok", result.Status);
        Assert.NotNull(result.Data["sprint_id"]);
        Assert.NotNull(result.Data["ticket_id"]);
    }

    [Fact]
    public async Task StartSprint_Twice_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("First", null, "medium");
        var result = await svc.StartSprintAsync("Second", null, "medium");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CloseSprint_NoActiveSprint_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.CloseSprintAsync();
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CloseSprint_NoTickets_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var sprintRepo = new SprintRepository(ctx);
        await sprintRepo.CreateAsync("SPRINT-0001");
        var result = await svc.CloseSprintAsync();
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CloseSprint_NonTerminalTicket_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("My ticket", null, "medium");

        var ticketRepo = new TicketRepository(ctx);
        var tickets = await ticketRepo.GetAllAsync();
        var ticket = tickets[0];

        var result = await svc.CloseSprintAsync();
        Assert.Equal("error", result.Status);
        Assert.Contains("Non-terminal", result.Message ?? "");
    }

    [Fact]
    public async Task CloseSprint_WithoutEval_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("My ticket", null, "medium");

        var ticketRepo = new TicketRepository(ctx);
        var tickets = await ticketRepo.GetAllAsync();
        var ticket = tickets[0];
        ticket.Status = SprintMcp.Domain.ValueObjects.TicketStatus.Closed;
        await ticketRepo.UpdateAsync(ticket);

        var result = await svc.CloseSprintAsync();
        Assert.Equal("error", result.Status);
        Assert.Contains("eval report missing", result.Message ?? "");
    }

    [Fact]
    public async Task CloseSprint_WithPassEvalAndMockChecker_Succeeds()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("My ticket", null, "medium");

        var ticketRepo = new TicketRepository(ctx);
        var tickets = await ticketRepo.GetAllAsync();
        var ticket = tickets[0];
        ticket.Status = SprintMcp.Domain.ValueObjects.TicketStatus.Closed;
        await ticketRepo.UpdateAsync(ticket);

        var evalRepo = new EvalReportRepository(ctx);
        await evalRepo.UpsertAsync(new EvalReport
        {
            TicketId = ticket.Id,
            RunId = "1234567890-test-run",
            Verdict = "pass",
            Content = "All good"
        });

        var result = await svc.CloseSprintAsync();
        Assert.Equal("ok", result.Status);
        Assert.Contains("closed successfully", (string)result.Data["message"]);

        // Verify MatchedRunTs was stamped
        var report = await evalRepo.GetByTicketIdAsync(ticket.Id);
        Assert.NotNull(report!.MatchedRunTs);

        // Verify sprint is closed
        var sprintRepo = new SprintRepository(ctx);
        var sprint = await sprintRepo.GetActiveAsync();
        Assert.Null(sprint);
    }

    [Fact]
    public async Task GetBoard_WithoutActive_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.GetBoardAsync();
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task GetBoard_WithActive_ReturnsTickets()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Board ticket", null, "low");

        var result = await svc.GetBoardAsync();
        Assert.Equal("ok", result.Status);
        Assert.NotNull(result.Data["tickets"]);
    }
}
