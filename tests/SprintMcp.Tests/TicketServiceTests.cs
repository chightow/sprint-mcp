using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.DTOs;
using SprintMcp.Application.Services;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Infrastructure.Persistence.Repositories;

namespace SprintMcp.Tests;

public class TicketServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TicketServiceTests()
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

    private TicketService CreateService(AppDbContext ctx, ISubagentRunChecker? checker = null)
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

    private async Task<Sprint> SetupSprintAsync(AppDbContext ctx, string phase = "planning")
    {
        var sprintRepo = new SprintRepository(ctx);
        var sprint = await sprintRepo.CreateNextAsync();
        while (phase != "planning" && sprint.Phase.Value != phase)
            sprint.AdvancePhase();
        if (phase != "planning")
            await sprintRepo.UpdateAsync(sprint);
        return sprint;
    }

    [Fact]
    public async Task GetTicket_NotFound_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.GetTicketAsync("TKT-9999");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CreateAndGetTicket_ReturnsTicketData()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx);
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test ticket", "Description", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.GetTicketAsync(ticket.Id);
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<TicketDetailResponse>(result.Data);
        Assert.Equal(ticket.Id, data.TicketId);
    }

    [Fact]
    public async Task UpdateStatus_ValidTransition_Succeeds()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "executing");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.UpdateStatusAsync(ticket.Id, "in_progress");
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<TicketStatusResponse>(result.Data);
        Assert.Equal("in_progress", data.NewStatus);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_ReturnsError()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "executing");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.UpdateStatusAsync(ticket.Id, "archived");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_ReturnsError()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "executing");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.UpdateStatusAsync(ticket.Id, "bogus");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task AddCriterion_AddsRow()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx);
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.AddCriterionAsync(ticket.Id, "Must work");
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<CriterionAddedResponse>(result.Data);
        Assert.Equal(1, data.Ordinal);
    }

    [Fact]
    public async Task CheckCriterion_TogglesSatisfied()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "executing");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var critRepo = new AcceptanceCriterionRepository(ctx);
        var crit = await critRepo.AddAsync(new AcceptanceCriterion(ticket.Id, 1, "Must work"));
        var svc = CreateService(ctx);
        var result = await svc.CheckCriterionAsync(ticket.Id, crit.Id, null);
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<CriterionCheckedResponse>(result.Data);
        Assert.True(data.Satisfied);
    }

    [Fact]
    public async Task SetPlan_UpdatesFields()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx);
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.SetPlanAsync(ticket.Id, "complex", "TDD", "src/foo.cs", true);
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<PlanSetResponse>(result.Data);
        Assert.Equal("complex", data.Tier);
        Assert.True(data.PlanApproved);

        var getResult = await svc.GetTicketAsync(ticket.Id);
        var getData = Assert.IsType<TicketDetailResponse>(getResult.Data);
        Assert.Equal("complex", getData.Tier);
    }

    [Fact]
    public async Task AddDecision_InsertsRow()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx);
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.AddDecisionAsync(ticket.Id, "Use SQLite", "It is simple");
        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public async Task AddTest_InsertsItem()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx);
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.AddTestAsync(ticket.Id, "Test the thing", "It works");
        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public async Task UpdateTest_ChangesStatus()
    {
        using var ctx = CreateContext();
        var sprint = await SetupSprintAsync(ctx, "planning");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        await svc.AddTestAsync(ticket.Id, "Test", "Works");
        sprint.AdvancePhase();
        var sprintRepo = new SprintRepository(ctx);
        await sprintRepo.UpdateAsync(sprint);
        var result = await svc.UpdateTestAsync(ticket.Id, 1, "pass");
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<TestUpdatedResponse>(result.Data);
        Assert.Equal("pass", data.Status);
    }

    [Fact]
    public async Task SetSummary_SavesProse()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "evaluating");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.SetSummaryAsync(ticket.Id, "Done well");
        Assert.Equal("ok", result.Status);
        var getResult = await svc.GetTicketAsync(ticket.Id);
        var getData = Assert.IsType<TicketDetailResponse>(getResult.Data);
        Assert.Equal("Done well", getData.Summary);
    }

    [Fact]
    public async Task SetEval_UpsertsReport()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "evaluating");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.SetEvalAsync(ticket.Id, "1234567890-test-run", "pass", "All good");
        Assert.Equal("ok", result.Status);
        var getResult = await svc.GetTicketAsync(ticket.Id);
        var getData = Assert.IsType<TicketDetailResponse>(getResult.Data);
        Assert.NotNull(getData.EvalReport);
    }

    [Fact]
    public async Task SetEval_InvalidVerdict_ReturnsError()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "evaluating");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.SetEvalAsync(ticket.Id, "1234567890-test-run", "bogus", "");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task SetEval_BadRunId_ReturnsError()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "evaluating");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);
        var svc = CreateService(ctx);
        var result = await svc.SetEvalAsync(ticket.Id, "bad-run-id", "pass", "");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CreateTicket_Valid_Succeeds()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx);
        var svc = CreateService(ctx);
        var result = await svc.CreateTicketAsync("New ticket", "A description", "high");
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<TicketCreatedResponse>(result.Data);
        Assert.NotNull(data.TicketId);
        Assert.Equal("New ticket", data.Title);
    }

    [Fact]
    public async Task CreateTicket_EmptyTitle_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.CreateTicketAsync("", "Desc", "medium");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CreateTicket_InvalidPriority_ReturnsError()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.CreateTicketAsync("Test", "Desc", "bogus");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task ListTickets_ReturnsAll()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var repo = new TicketRepository(ctx);
        await repo.CreateAsync("First", "Desc", Priority.Medium);
        await repo.CreateAsync("Second", "Desc", Priority.Medium);

        var result = await svc.ListTicketsAsync();
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<TicketListResponse>(result.Data);
        Assert.Equal(2, data.Tickets.Count);
    }

    [Fact]
    public async Task ListTickets_Empty_ReturnsEmptyList()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.ListTicketsAsync();
        Assert.Equal("ok", result.Status);
        var data = Assert.IsType<TicketListResponse>(result.Data);
        Assert.Empty(data.Tickets);
    }

    [Fact]
    public async Task PhaseGate_BlocksWrongPhase()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "executing");
        var svc = CreateService(ctx);
        var result = await svc.CreateTicketAsync("Test", "Desc", "medium");
        Assert.Equal("error", result.Status);
        Assert.Contains("planning", result.Message ?? "");
    }

    [Fact]
    public async Task PhaseGate_NoActiveSprint_Blocks()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.CreateTicketAsync("Test", "Desc", "medium");
        Assert.Equal("error", result.Status);
        Assert.Contains("No active sprint", result.Message ?? "");
    }

    [Fact]
    public async Task IdempotencyKey_ReturnsCachedResult()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx);
        var svc = CreateService(ctx);
        var first = await svc.CreateTicketAsync("Idempotent", "Desc", "medium", "key-1");
        Assert.Equal("ok", first.Status);

        var second = await svc.CreateTicketAsync("Idempotent", "Desc", "medium", "key-1");
        Assert.Equal("ok", second.Status);
        var firstData = first.GetData<TicketCreatedResponse>();
        var secondData = second.GetData<TicketCreatedResponse>();
        Assert.NotNull(firstData);
        Assert.NotNull(secondData);
        Assert.Equal(firstData!.TicketId, secondData!.TicketId);
    }

    [Fact]
    public async Task FieldLimit_Title_BlocksOversize()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx);
        var svc = CreateService(ctx);
        var longTitle = new string('X', 201);
        var result = await svc.CreateTicketAsync(longTitle, "Desc", "medium");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CanTransitionTo_OpenToInProgress_Allowed()
    {
        var result = TicketStatus.Open.CanTransitionTo(TicketStatus.InProgress);
        Assert.True(result);
    }

    [Fact]
    public async Task CanTransitionTo_ArchivedToAny_Blocked()
    {
        Assert.False(TicketStatus.Archived.CanTransitionTo(TicketStatus.Closed));
        Assert.False(TicketStatus.Archived.CanTransitionTo(TicketStatus.InProgress));
        Assert.False(TicketStatus.Archived.CanTransitionTo(TicketStatus.Open));
    }

    [Fact]
    public async Task SetEval_SubagentRunNotFound_ReturnsError()
    {
        using var ctx = CreateContext();
        await SetupSprintAsync(ctx, "evaluating");
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc", Priority.Medium);

        var mock = new Mock<ISubagentRunChecker>();
        mock.Setup(m => m.CheckRunAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var svc = CreateService(ctx, mock.Object);

        var result = await svc.SetEvalAsync(ticket.Id, "1234567890-test-run", "pass", "All good");
        Assert.Equal("error", result.Status);
    }
}
