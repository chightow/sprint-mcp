using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using SprintMcp.Application;
using SprintMcp.Infrastructure;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Server.Handlers;

var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
var dbPath = Path.Combine(projectRoot, ".tickets", "sprint.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddSingleton(projectRoot)
    .AddInfrastructure(dbPath)
    .AddApplication()
    .AddMcpServer()
    .WithTools<TicketHandler>()
    .WithTools<SprintHandler>()
    .WithStdioServerTransport();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await app.RunAsync();

static string FindProjectRoot(string start)
{
    var d = Path.GetFullPath(start);
    for (var depth = 0; depth < 50; depth++)
    {
        if (Directory.Exists(Path.Combine(d, ".git")) ||
            Directory.Exists(Path.Combine(d, ".tickets")))
            return d;

        var parent = Path.GetDirectoryName(d);
        if (parent is null || parent == d) return start;
        d = parent;
    }
    return start;
}
