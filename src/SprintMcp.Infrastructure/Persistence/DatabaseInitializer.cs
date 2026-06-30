using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace SprintMcp.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "SprintMcp.Infrastructure.Persistence.schema.sql";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        await db.Database.ExecuteSqlRawAsync(sql);
    }

    public static void Initialize(AppDbContext db) => InitializeAsync(db).GetAwaiter().GetResult();
}
