using Microsoft.Extensions.DependencyInjection;
using SprintMcp.Application.Abstractions;
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
        return services;
    }
}
