using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SprintMcp.Application.Abstractions;
using SprintMcp.Domain.Repositories;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Infrastructure.Persistence.Repositories;

namespace SprintMcp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string dbPath)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Foreign Keys=True;"));

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAcceptanceCriterionRepository, AcceptanceCriterionRepository>();
        services.AddScoped<IDecisionRepository, DecisionRepository>();
        services.AddScoped<ITestPlanItemRepository, TestPlanItemRepository>();
        services.AddScoped<IEvalReportRepository, EvalReportRepository>();
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<ISprintHandoffRepository, SprintHandoffRepository>();
        services.AddScoped<IActiveTaskRepository, ActiveTaskRepository>();
        services.AddScoped<IEventStore, EventStore>();

        services.AddScoped<ISubagentRunChecker, SubagentRunChecker>();
        services.AddScoped<ITransactionManager, TransactionManager>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddScoped<IInvariantContext, InvariantContext>();

        return services;
    }
}
