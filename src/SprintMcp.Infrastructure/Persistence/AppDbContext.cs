using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<AcceptanceCriterion> AcceptanceCriteria => Set<AcceptanceCriterion>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<TestPlanItem> TestPlanItems => Set<TestPlanItem>();
    public DbSet<EvalReport> EvalReports => Set<EvalReport>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<SprintHandoff> SprintHandoffs => Set<SprintHandoff>();
    public DbSet<ActiveTask> ActiveTasks => Set<ActiveTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var statusConverter = new ValueConverter<TicketStatus, string>(
            v => v.Value, v => TicketStatus.FromString(v));

        var priorityConverter = new ValueConverter<Priority, string>(
            v => v.Value, v => Priority.FromString(v));

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("TEXT");
            entity.Property(e => e.Title).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Description).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Status).IsRequired().HasColumnType("TEXT").HasConversion(statusConverter);
            entity.Property(e => e.Priority).IsRequired().HasColumnType("TEXT").HasConversion(priorityConverter);
            entity.Property(e => e.Tier).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.SprintId).HasColumnType("TEXT");
            entity.Property(e => e.PlanApproach).HasColumnType("TEXT");
            entity.Property(e => e.PlanFiles).HasColumnType("TEXT");
            entity.Property(e => e.PlanApprovedAt).HasColumnType("TEXT");
            entity.Property(e => e.Summary).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.UpdatedAt).IsRequired().HasColumnType("TEXT");

            entity.ToTable(t => t.HasCheckConstraint("CK_Ticket_Status", "Status IN ('open','in_progress','closed','cancelled','archived')"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Ticket_Priority", "Priority IN ('low','medium','high','critical')"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Ticket_Tier", "Tier IN ('trivial','regular','complex')"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Ticket_Id", "Id GLOB 'TKT-[0-9][0-9][0-9][0-9]'"));
        });

        modelBuilder.Entity<AcceptanceCriterion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketId).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Ordinal).IsRequired();
            entity.Property(e => e.Text).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Satisfied).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("TEXT");
            entity.HasIndex(e => new { e.TicketId, e.Ordinal }).IsUnique();
        });

        modelBuilder.Entity<Decision>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketId).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Title).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Rationale).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("TEXT");
            entity.HasIndex(e => new { e.TicketId, e.Title }).IsUnique();
        });

        modelBuilder.Entity<TestPlanItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketId).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Ordinal).IsRequired();
            entity.Property(e => e.Description).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Expected).HasColumnType("TEXT");
            entity.Property(e => e.Status).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.UpdatedAt).IsRequired().HasColumnType("TEXT");
            entity.HasIndex(e => new { e.TicketId, e.Ordinal }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_TestPlan_Status", "Status IN ('pending','pass','fail','blocked')"));
        });

        modelBuilder.Entity<EvalReport>(entity =>
        {
            entity.HasKey(e => e.TicketId);
            entity.Property(e => e.TicketId).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.RunId).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Verdict).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Content).HasColumnType("TEXT");
            entity.Property(e => e.MatchedRunTs).HasColumnType("TEXT");
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.UpdatedAt).IsRequired().HasColumnType("TEXT");
            entity.ToTable(t => t.HasCheckConstraint("CK_EvalReport_Verdict", "Verdict IN ('pass','fail','pending')"));
        });

        modelBuilder.Entity<Sprint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnType("TEXT");
            entity.Property(e => e.Status).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.StartedAt).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.ClosedAt).HasColumnType("TEXT");
            entity.ToTable(t => t.HasCheckConstraint("CK_Sprint_Status", "Status IN ('active','closed')"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Sprint_Id", "Id GLOB 'SPRINT-[0-9][0-9][0-9][0-9]'"));
        });

        modelBuilder.Entity<SprintHandoff>(entity =>
        {
            entity.HasKey(e => e.SprintId);
            entity.Property(e => e.SprintId).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.CurrentFocus).HasColumnType("TEXT");
            entity.Property(e => e.InProgress).HasColumnType("TEXT");
            entity.Property(e => e.Discoveries).HasColumnType("TEXT");
            entity.Property(e => e.NextSteps).HasColumnType("TEXT");
            entity.Property(e => e.UpdatedAt).IsRequired().HasColumnType("TEXT");
        });

        modelBuilder.Entity<ActiveTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SprintId).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.TaskRef).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Ordinal).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired().HasColumnType("TEXT");
            entity.HasIndex(e => new { e.SprintId, e.Ordinal }).IsUnique();
        });
    }
}
