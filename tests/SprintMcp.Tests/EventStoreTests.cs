using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SprintMcp.Application.Services;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Infrastructure.Persistence.Repositories;

namespace SprintMcp.Tests;

public class EventStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public EventStoreTests()
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
    public async Task AppendAsync_SavesAndReturnsEventWithId()
    {
        var ctx = CreateContext();
        var store = new EventStore(ctx);

        var evt = new Event("TicketCreated", "domain", "ticket", "TKT-0001",
            null, ["ledger:abc"], DateTime.UtcNow, """{"ticket_id":"TKT-0001"}""");

        var saved = await store.AppendAsync(evt);

        Assert.True(saved.Id > 0);
        Assert.Equal("TicketCreated", saved.EventType);
        Assert.Equal("domain", saved.Category);
    }

    [Fact]
    public async Task AppendAsync_SequentialEventsGetIncrementalIds()
    {
        var ctx = CreateContext();
        var store = new EventStore(ctx);

        var e1 = await store.AppendAsync(new Event("TicketCreated", "domain", "ticket", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));
        var e2 = await store.AppendAsync(new Event("TicketStatusChanged", "domain", "ticket", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));

        Assert.Equal(e1.Id + 1, e2.Id);
    }

    [Fact]
    public async Task GetSinceAsync_ReturnsEventsAfterCursor()
    {
        var ctx = CreateContext();
        var store = new EventStore(ctx);

        var e1 = await store.AppendAsync(new Event("TicketCreated", "domain", "ticket", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));
        var e2 = await store.AppendAsync(new Event("StatusChanged", "domain", "ticket", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));
        var e3 = await store.AppendAsync(new Event("FileWrite", "agent", "agent_action", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));

        var result = await store.GetSinceAsync(e1.Id, 100);

        Assert.Equal(2, result.Count);
        Assert.Equal(e2.Id, result[0].Id);
        Assert.Equal(e3.Id, result[1].Id);
    }

    [Fact]
    public async Task GetSinceAsync_FiltersByType()
    {
        var ctx = CreateContext();
        var store = new EventStore(ctx);

        await store.AppendAsync(new Event("TicketCreated", "domain", "ticket", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));
        await store.AppendAsync(new Event("FileWrite", "agent", "agent_action", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));

        var result = await store.GetSinceAsync(0, 100, typeFilter: "FileWrite");

        Assert.Single(result);
        Assert.Equal("FileWrite", result[0].EventType);
    }

    [Fact]
    public async Task GetSinceAsync_FiltersByAggregateId()
    {
        var ctx = CreateContext();
        var store = new EventStore(ctx);

        await store.AppendAsync(new Event("TicketCreated", "domain", "ticket", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));
        await store.AppendAsync(new Event("FileWrite", "agent", "agent_action", "TKT-0002",
            null, [], DateTime.UtcNow, "{}"));

        var result = await store.GetSinceAsync(0, 100, aggregateIdFilter: "TKT-0002");

        Assert.Single(result);
        Assert.Equal("TKT-0002", result[0].AggregateId);
    }

    [Fact]
    public async Task GetMaxIdAsync_ReturnsZeroWhenEmpty()
    {
        var ctx = CreateContext();
        var store = new EventStore(ctx);

        var maxId = await store.GetMaxIdAsync();

        Assert.Equal(0, maxId);
    }

    [Fact]
    public async Task GetMaxIdAsync_ReturnsMaxId()
    {
        var ctx = CreateContext();
        var store = new EventStore(ctx);

        await store.AppendAsync(new Event("A", "domain", "ticket", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));
        var e2 = await store.AppendAsync(new Event("B", "domain", "ticket", "TKT-0001",
            null, [], DateTime.UtcNow, "{}"));

        var maxId = await store.GetMaxIdAsync();

        Assert.Equal(e2.Id, maxId);
    }

    [Fact]
    public async Task Track_SavesWithNextSaveChangesAsync()
    {
        var ctx = CreateContext();
        var store = new EventStore(ctx);
        var ticketRepo = new TicketRepository(ctx);

        var ticket = await ticketRepo.CreateAsync("Test", "", Priority.Medium);

        var evt = new Event("TicketCreated", "domain", "ticket", ticket.Id,
            null, ["ledger:ref1"], DateTime.UtcNow, "{}");
        store.Track(evt);

        await ticketRepo.UpdateAsync(ticket);

        Assert.True(evt.Id > 0);
        var fromDb = await ctx.Events.FirstOrDefaultAsync(e => e.Id == evt.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("TicketCreated", fromDb!.EventType);
    }

    [Fact]
    public void GetCausedBy_ReturnsArrayFromJson()
    {
        var evt = new Event("Test", "domain", "ticket", "TKT-0001",
            null, ["ledger:abc", "ledger:def"], DateTime.UtcNow, "{}");

        var causedBy = evt.GetCausedBy();

        Assert.Equal(2, causedBy.Length);
        Assert.Contains("ledger:abc", causedBy);
        Assert.Contains("ledger:def", causedBy);
    }
}
