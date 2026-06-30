using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.Services;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;
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
        DatabaseInitializer.Initialize(ctx);
    }

    public void Dispose() => _connection.Close();

    private AppDbContext CreateContext() => new(_options);

    private SprintService CreateService(AppDbContext ctx, ISubagentRunChecker? checker = null, ITransactionManager? txManager = null)
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
            txManager ?? new MockTransactionManager(),
            Mock.Of<ILogger<SprintService>>(),
            ".",
            TimeProvider.System,
            new SprintLock());
    }

    private async Task AdvanceToEvaluating(SprintService svc)
    {
        var r1 = await svc.AdvancePhaseAsync();
        Assert.Equal("ok", r1.Status);
        var r2 = await svc.AdvancePhaseAsync();
        Assert.Equal("ok", r2.Status);
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
        var sprint = await sprintRepo.CreateNextAsync();
        // Advance to evaluating
        sprint = await sprintRepo.GetByIdAsync(sprint.Id);
        sprint!.AdvancePhase();
        sprint.AdvancePhase();
        await sprintRepo.UpdateAsync(sprint);

        var result = await svc.CloseSprintAsync();
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CloseSprint_NonTerminalTicket_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("My ticket", null, "medium");
        await AdvanceToEvaluating(svc);

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
        await AdvanceToEvaluating(svc);

        var ticketRepo = new TicketRepository(ctx);
        var tickets = await ticketRepo.GetAllAsync();
        var ticket = tickets[0];
        ticket.ChangeStatus(TicketStatus.InProgress);
        ticket.ChangeStatus(TicketStatus.Closed);
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
        await AdvanceToEvaluating(svc);

        var ticketRepo = new TicketRepository(ctx);
        var tickets = await ticketRepo.GetAllAsync();
        var ticket = tickets[0];
        ticket.ChangeStatus(TicketStatus.InProgress);
        ticket.ChangeStatus(TicketStatus.Closed);
        await ticketRepo.UpdateAsync(ticket);

        var evalRepo = new EvalReportRepository(ctx);
        await evalRepo.UpsertAsync(new EvalReport(ticket.Id, "1234567890-test-run", Verdict.Pass, "All good"));

        var result = await svc.CloseSprintAsync();
        Assert.Equal("ok", result.Status);
        Assert.Contains("closed successfully", (string)result.Data["message"]);

        var report = await evalRepo.GetByTicketIdAsync(ticket.Id);
        Assert.NotNull(report!.MatchedRunTs);

        var sprintRepo = new SprintRepository(ctx);
        var sprint = await sprintRepo.GetActiveAsync();
        Assert.Null(sprint);
    }

    [Fact]
    public async Task CloseSprint_WrongPhase_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("My ticket", null, "medium");

        var result = await svc.CloseSprintAsync();
        Assert.Equal("error", result.Status);
        Assert.Contains("evaluating", result.Message ?? "");
    }

    [Fact]
    public async Task AdvancePhase_PlanningToExecuting()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");

        var result = await svc.AdvancePhaseAsync();
        Assert.Equal("ok", result.Status);

        var board = await svc.GetBoardAsync();
        Assert.Equal("executing", board.Data["phase"]);
    }

    [Fact]
    public async Task AdvancePhase_ExecutingToEvaluating()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");
        await svc.AdvancePhaseAsync();

        var result = await svc.AdvancePhaseAsync();
        Assert.Equal("ok", result.Status);

        var board = await svc.GetBoardAsync();
        Assert.Equal("evaluating", board.Data["phase"]);
    }

    [Fact]
    public async Task AdvancePhase_Evaluating_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");
        await AdvanceToEvaluating(svc);

        var result = await svc.AdvancePhaseAsync();
        Assert.Equal("error", result.Status);
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

    [Fact]
    public async Task GetBoard_IncludesLockState()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Board ticket", null, "low");

        var result = await svc.GetBoardAsync();
        Assert.Equal("ok", result.Status);
        Assert.Contains("lock_held", result.Data);
        Assert.Contains("lock_held_since", result.Data);
    }

    [Fact]
    public async Task UpdateHandoff_NoActiveSprint_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.UpdateHandoffAsync("focus", "ip", "disc", "next");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task UpdateHandoff_CreatesNew()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");

        var result = await svc.UpdateHandoffAsync("Focus on X", "Working on Y", "Found Z", "Do A");
        Assert.Equal("ok", result.Status);
        Assert.Equal("Focus on X", result.Data["current_focus"]);
        Assert.Equal("Working on Y", result.Data["in_progress"]);
        Assert.Equal("Found Z", result.Data["discoveries"]);
        Assert.Equal("Do A", result.Data["next_steps"]);
    }

    [Fact]
    public async Task UpdateHandoff_UpdatesExisting()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");
        await svc.UpdateHandoffAsync("Old focus", null, null, null);

        var result = await svc.UpdateHandoffAsync("New focus", null, null, null);
        Assert.Equal("ok", result.Status);
        Assert.Equal("New focus", result.Data["current_focus"]);
        Assert.Equal("", result.Data["in_progress"]);
    }

    [Fact]
    public async Task AddTask_NoActiveSprint_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.AddActiveTaskAsync("TKT-0001");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task AddTask_EmptyRef_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");
        var result = await svc.AddActiveTaskAsync(" ");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task AddTask_Valid_Succeeds()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");
        var result = await svc.AddActiveTaskAsync("TKT-0001");
        Assert.Equal("ok", result.Status);
        Assert.Equal("TKT-0001", result.Data["task_ref"]);
    }

    [Fact]
    public async Task RemoveTask_NoActiveSprint_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.RemoveActiveTaskAsync(1);
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task RemoveTask_InvalidId_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");
        var result = await svc.RemoveActiveTaskAsync(0);
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task RemoveTask_Valid_Succeeds()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("Test", null, "medium");
        var add = await svc.AddActiveTaskAsync("TKT-0001");
        var taskId = (int)add.Data["task_id"];

        var result = await svc.RemoveActiveTaskAsync(taskId);
        Assert.Equal("ok", result.Status);
        Assert.Equal(taskId, result.Data["removed_task_id"]);
    }

    [Fact]
    public async Task CloseSprint_WithAlreadyMatchedRunTs_SkipsSubagentCheck()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        await svc.StartSprintAsync("My ticket", null, "medium");
        await AdvanceToEvaluating(svc);

        var ticketRepo = new TicketRepository(ctx);
        var tickets = await ticketRepo.GetAllAsync();
        var ticket = tickets[0];
        ticket.ChangeStatus(TicketStatus.InProgress);
        ticket.ChangeStatus(TicketStatus.Closed);
        await ticketRepo.UpdateAsync(ticket);

        var evalRepo = new EvalReportRepository(ctx);
        var evalReport = new EvalReport(ticket.Id, "1234567890-test-run", Verdict.Pass, "All good")
        {
            MatchedRunTs = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await evalRepo.UpsertAsync(evalReport);

        // Subagent check would return false, but MatchedRunTs already set → skipped
        var mock = new Mock<ISubagentRunChecker>();
        mock.Setup(m => m.CheckRunAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var svcWithMock = CreateService(ctx, mock.Object);

        var result = await svcWithMock.CloseSprintAsync();
        Assert.Equal("ok", result.Status);
        Assert.Contains("closed successfully", (string)result.Data["message"]);

        var sprintRepo = new SprintRepository(ctx);
        var active = await sprintRepo.GetActiveAsync();
        Assert.Null(active);
    }
}
