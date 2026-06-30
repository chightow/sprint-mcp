using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SprintMcp.Application.Services;
using SprintMcp.Domain.Entities;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Infrastructure.Persistence.Repositories;

namespace SprintMcp.Tests;

public class TicketServiceTests : IDisposable
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
        using var ctx = new AppDbContext(_options);
        DatabaseInitializer.Initialize(ctx);
    }

    public void Dispose() => _connection.Close();

    private AppDbContext CreateContext() => new(_options);

    private TicketService CreateService(AppDbContext ctx) => new(
        new TicketRepository(ctx),
        new AcceptanceCriterionRepository(ctx),
        new DecisionRepository(ctx),
        new TestPlanItemRepository(ctx),
        new EvalReportRepository(ctx),
        Mock.Of<ILogger<TicketService>>());

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
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test ticket", "Description");
        var svc = CreateService(ctx);
        var result = await svc.GetTicketAsync(ticket.Id);
        Assert.Equal("ok", result.Status);
        Assert.Equal(ticket.Id, result.Data["ticket_id"]);
    }

    [Fact]
    public async Task UpdateStatus_ValidTransition_Succeeds()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.UpdateStatusAsync(ticket.Id, "closed");
        Assert.Equal("ok", result.Status);
        Assert.Equal("closed", result.Data["new_status"]);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_ReturnsError()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.UpdateStatusAsync(ticket.Id, "bogus");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task AddCriterion_AddsRow()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.AddCriterionAsync(ticket.Id, "Must work");
        Assert.Equal("ok", result.Status);
        Assert.Equal(1, result.Data["ordinal"]);
    }

    [Fact]
    public async Task CheckCriterion_TogglesSatisfied()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var critRepo = new AcceptanceCriterionRepository(ctx);
        var crit = await critRepo.AddAsync(new AcceptanceCriterion
        {
            TicketId = ticket.Id, Ordinal = 1, Text = "Must work"
        });
        var svc = CreateService(ctx);
        var result = await svc.CheckCriterionAsync(ticket.Id, crit.Id, null);
        Assert.Equal("ok", result.Status);
        Assert.True((bool)result.Data["satisfied"]);
    }

    [Fact]
    public async Task SetPlan_UpdatesFields()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.SetPlanAsync(ticket.Id, "complex", "TDD", "src/foo.cs", true);
        Assert.Equal("ok", result.Status);
        var getResult = await svc.GetTicketAsync(ticket.Id);
        Assert.Equal("complex", getResult.Data["tier"]);
    }

    [Fact]
    public async Task AddDecision_InsertsRow()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.AddDecisionAsync(ticket.Id, "Use SQLite", "It is simple");
        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public async Task AddTest_InsertsItem()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.AddTestAsync(ticket.Id, "Test the thing", "It works");
        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public async Task UpdateTest_ChangesStatus()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        await svc.AddTestAsync(ticket.Id, "Test", "Works");
        var result = await svc.UpdateTestAsync(ticket.Id, 1, "pass");
        Assert.Equal("ok", result.Status);
    }

    [Fact]
    public async Task SetSummary_SavesProse()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.SetSummaryAsync(ticket.Id, "Done well");
        Assert.Equal("ok", result.Status);
        var getResult = await svc.GetTicketAsync(ticket.Id);
        Assert.Equal("Done well", getResult.Data["summary"]);
    }

    [Fact]
    public async Task SetEval_UpsertsReport()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.SetEvalAsync(ticket.Id, "123456-foo", "pass", "All good");
        Assert.Equal("ok", result.Status);
        var getResult = await svc.GetTicketAsync(ticket.Id);
        Assert.NotNull(getResult.Data["eval_report"]);
    }

    [Fact]
    public async Task SetEval_InvalidVerdict_ReturnsError()
    {
        using var ctx = CreateContext();
        var repo = new TicketRepository(ctx);
        var ticket = await repo.CreateAsync("Test", "Desc");
        var svc = CreateService(ctx);
        var result = await svc.SetEvalAsync(ticket.Id, "run-1", "bogus", "");
        Assert.Equal("error", result.Status);
    }

    [Fact]
    public async Task CreateTicket_Valid_Succeeds()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.CreateTicketAsync("New ticket", "A description", "high");
        Assert.Equal("ok", result.Status);
        Assert.NotNull(result.Data["ticket_id"]);
        Assert.Equal("New ticket", result.Data["title"]);
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
        await repo.CreateAsync("First", "Desc");
        await repo.CreateAsync("Second", "Desc");

        var result = await svc.ListTicketsAsync();
        Assert.Equal("ok", result.Status);
        var tickets = (List<object>)result.Data["tickets"];
        Assert.Equal(2, tickets.Count);
    }

    [Fact]
    public async Task ListTickets_Empty_ReturnsEmptyList()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var result = await svc.ListTicketsAsync();
        Assert.Equal("ok", result.Status);
        var tickets = (List<object>)result.Data["tickets"];
        Assert.Empty(tickets);
    }
}
