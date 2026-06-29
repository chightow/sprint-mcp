using Microsoft.Extensions.DependencyInjection;
using SprintMcp.Application.Services;

namespace SprintMcp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TicketService>();
        services.AddScoped<SprintService>();
        return services;
    }
}
