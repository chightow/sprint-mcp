using Microsoft.Extensions.DependencyInjection;
using SprintMcp.Application.Abstractions;
using SprintMcp.Application.Invariants;
using SprintMcp.Application.Services;

namespace SprintMcp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITicketLock, TicketLock>();
        services.AddSingleton<ISprintLock, SprintLock>();
        services.AddScoped<IdempotencyService>();
        services.AddScoped<TicketService>();
        services.AddScoped<SprintService>();
        services.AddScoped<EventService>();

        services.AddScoped<InvariantEngine>();
        services.AddScoped<IInvariant, PhaseGateInvariant>();
        services.AddScoped<IInvariant, TicketStatusTransitionInvariant>();

        return services;
    }
}
