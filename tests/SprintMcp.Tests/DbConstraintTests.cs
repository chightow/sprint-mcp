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
        DatabaseInitializer.Initialize(ctx);
    }

    public void Dispose() => _connection.Close();
    private AppDbContext Ctx() => new(_options);

    [Fact]
    public void Ticket_BadStatus_Throws()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<SqliteException>(() =>
            ctx.Database.ExecuteSqlRaw("INSERT INTO Tickets (Id, Title, Description, Status, Priority, Tier, PlanApproach, PlanFiles, Summary, CreatedAt, UpdatedAt) VALUES ('TKT-0001', 'Test', 'Desc', 'bogus', 'medium', 'regular', '', '', '', '2024-01-01', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ticket_BadTier_Throws()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<SqliteException>(() =>
            ctx.Database.ExecuteSqlRaw("INSERT INTO Tickets (Id, Title, Description, Status, Priority, Tier, PlanApproach, PlanFiles, Summary, CreatedAt, UpdatedAt) VALUES ('TKT-0002', 'Test', 'Desc', 'open', 'medium', 'invalid', '', '', '', '2024-01-01', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ticket_BadIdFormat_Throws()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<SqliteException>(() =>
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

        var ex = await Assert.ThrowsAsync<SqliteException>(() =>
            ctx.Database.ExecuteSqlRawAsync("INSERT INTO TestPlanItems (TicketId, Ordinal, Description, Expected, Status, UpdatedAt) VALUES ('TKT-0020', 1, 'Test', '', 'invalid', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvalReport_BadVerdict_Throws()
    {
        using var ctx = Ctx();
        ctx.Tickets.Add(new Ticket("TKT-0030", "Test", "Desc"));
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SqliteException>(() =>
            ctx.Database.ExecuteSqlRawAsync("INSERT INTO EvalReports (TicketId, RunId, Verdict, Content, CreatedAt, UpdatedAt) VALUES ('TKT-0030', 'run-1', 'invalid', '', '2024-01-01', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvalReport_ShortRunId_Throws()
    {
        using var ctx = Ctx();
        ctx.Tickets.Add(new Ticket("TKT-0031", "Test", "Desc"));
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SqliteException>(() =>
            ctx.Database.ExecuteSqlRawAsync("INSERT INTO EvalReports (TicketId, RunId, Verdict, Content, CreatedAt, UpdatedAt) VALUES ('TKT-0031', 'ab', 'pass', '', '2024-01-01', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sprint_BadStatus_Throws()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<SqliteException>(() =>
            ctx.Database.ExecuteSqlRaw("INSERT INTO Sprints (Id, Status, StartedAt) VALUES ('SPRINT-0001', 'invalid', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sprint_BadPhase_Throws()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<SqliteException>(() =>
            ctx.Database.ExecuteSqlRaw("INSERT INTO Sprints (Id, Status, Phase, StartedAt) VALUES ('SPRINT-0002', 'active', 'bogus', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sprint_BadIdFormat_Throws()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<SqliteException>(() =>
            ctx.Database.ExecuteSqlRaw("INSERT INTO Sprints (Id, Status, StartedAt) VALUES ('BAD-ID', 'active', '2024-01-01')"));
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChildRow_OrphanTicketId_ThrowsForeignKey()
    {
        using var ctx = Ctx();
        var ex = Assert.Throws<SqliteException>(() =>
            ctx.Database.ExecuteSqlRaw("INSERT INTO AcceptanceCriteria (TicketId, Ordinal, Text, Satisfied, CreatedAt) VALUES ('TKT-9999', 1, 'Orphan', 0, '2024-01-01')"));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ticket_Delete_CascadesToChildrenAtDbLevel()
    {
        using var ctx = Ctx();
        ctx.Tickets.Add(new Ticket("TKT-0040", "Cascade", "Desc"));
        await ctx.SaveChangesAsync();
        ctx.AcceptanceCriteria.Add(new AcceptanceCriterion { TicketId = "TKT-0040", Ordinal = 1, Text = "Child" });
        await ctx.SaveChangesAsync();

        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM Tickets WHERE Id = 'TKT-0040'");

        using var verify = Ctx();
        Assert.Empty(verify.AcceptanceCriteria.Where(c => c.TicketId == "TKT-0040").ToList());
    }
}
