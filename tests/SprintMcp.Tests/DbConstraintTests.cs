using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Infrastructure.Persistence;

namespace SprintMcp.Tests;

public class DbConstraintTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DbConstraintTests()
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
    private AppDbContext Ctx() => new(_options);

    [Fact]
    public void Ticket_BadStatus_Throws()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            ctx.Database.ExecuteSqlRaw("INSERT INTO Tickets (Id, Title, Description, Status, Priority, Tier, PlanApproach, PlanFiles, Summary, CreatedAt, UpdatedAt) VALUES ('TKT-0001', 'Test', 'Desc', 'bogus', 'medium', 'regular', '', '', '', '2024-01-01', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ticket_BadTier_Throws()
    {
        using var ctx = Ctx();
        ctx.Tickets.Add(new Ticket("TKT-0002", "Test", "Desc") { Tier = "invalid" });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.Contains("CHECK", ex.InnerException?.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ticket_BadIdFormat_Throws()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            ctx.Database.ExecuteSqlRaw("INSERT INTO Tickets (Id, Title, Description, Status, Priority, Tier, PlanApproach, PlanFiles, Summary, CreatedAt, UpdatedAt) VALUES ('bad-id', 'Test', 'Desc', 'open', 'medium', 'regular', '', '', '', '2024-01-01', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptanceCriteria_DuplicateOrdinal_Throws()
    {
        using var ctx = Ctx();
        ctx.Tickets.Add(new Ticket("TKT-0010", "Test", "Desc"));
        await ctx.SaveChangesAsync();

        ctx.AcceptanceCriteria.Add(new AcceptanceCriterion
        {
            TicketId = "TKT-0010", Ordinal = 1, Text = "First"
        });
        ctx.AcceptanceCriteria.Add(new AcceptanceCriterion
        {
            TicketId = "TKT-0010", Ordinal = 1, Text = "Duplicate"
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.Contains("UNIQUE", ex.InnerException?.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestPlanItem_BadStatus_Throws()
    {
        using var ctx = Ctx();
        ctx.Tickets.Add(new Ticket("TKT-0020", "Test", "Desc"));
        await ctx.SaveChangesAsync();

        ctx.TestPlanItems.Add(new TestPlanItem
        {
            TicketId = "TKT-0020", Ordinal = 1, Description = "Test", Status = "invalid"
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.Contains("CHECK", ex.InnerException?.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvalReport_BadVerdict_Throws()
    {
        using var ctx = Ctx();
        ctx.Tickets.Add(new Ticket("TKT-0030", "Test", "Desc"));
        await ctx.SaveChangesAsync();

        ctx.EvalReports.Add(new EvalReport
        {
            TicketId = "TKT-0030", RunId = "run-1", Verdict = "invalid"
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.Contains("CHECK", ex.InnerException?.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sprint_BadStatus_Throws()
    {
        using var ctx = Ctx();
        ctx.Sprints.Add(new Sprint("SPRINT-0001") { Status = "invalid" });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.Contains("CHECK", ex.InnerException?.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sprint_BadIdFormat_Throws()
    {
        using var ctx = Ctx();
        ctx.Sprints.Add(new Sprint("BAD-ID"));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.Contains("CHECK", ex.InnerException?.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }
}
