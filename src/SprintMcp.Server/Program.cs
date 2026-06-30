using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using ModelContextProtocol;
using SprintMcp.Application;
using SprintMcp.Application.Abstractions;
using SprintMcp.Infrastructure;
using SprintMcp.Infrastructure.Persistence;
using SprintMcp.Server;
using SprintMcp.Server.Handlers;

var projectRoot = FindProjectRoot(Directory.GetCurrentDirectory());
var configuredPath = Environment.GetEnvironmentVariable("SPRINTMCP_DB_PATH");
var dbPath = configuredPath ?? Path.Combine(projectRoot, ".tickets", "sprint.db");
var agentId = Environment.GetEnvironmentVariable("SPRINTMCP_AGENT_ID") ?? "unknown";
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ConsoleLoggerOptions>(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddSingleton<IAgentContext>(_ => new AgentContext(agentId))
    .AddSingleton(projectRoot)
    .AddInfrastructure(dbPath)
    .AddApplication()
    .AddMcpServer()
    .WithTools<TicketHandler>()
    .WithTools<SprintHandler>()
    .WithTools<EventToolHandler>()
    .WithStdioServerTransport();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseInitializer.InitializeAsync(db);
}

await app.RunAsync();

static string FindProjectRoot(string start)
{
    var d = Path.GetFullPath(start);
    while (true)
    {
        if (Directory.Exists(Path.Combine(d, ".tickets")) || Directory.Exists(Path.Combine(d, ".git")))
            return d;
        var parent = Path.GetDirectoryName(d);
        if (parent is null || parent == d) break;
        d = parent;
    }
    return start;
}
