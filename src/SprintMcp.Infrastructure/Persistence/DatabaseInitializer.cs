using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace SprintMcp.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;");

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("schema.sql", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        foreach (var stmt in sql.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = stmt.Trim();
            if (trimmed.Length == 0) continue;
            await db.Database.ExecuteSqlRawAsync(trimmed);
        }
    }

    public static void Initialize(AppDbContext db) => InitializeAsync(db).GetAwaiter().GetResult();
}
