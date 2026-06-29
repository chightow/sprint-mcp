using Microsoft.EntityFrameworkCore;
using SprintMcp.Domain.Entities;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Infrastructure.Persistence.Repositories;

public class SprintRepository(AppDbContext db) : ISprintRepository
{
    public async Task<Sprint?> GetActiveAsync()
    {
        return await db.Sprints.FirstOrDefaultAsync(s => s.Status == "active");
    }

    public async Task<Sprint?> GetByIdAsync(string sprintId)
    {
        return await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId);
    }

    public async Task<Sprint> CreateAsync(string id)
    {
        var sprint = new Sprint(id);
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync();
        return sprint;
    }

    public async Task UpdateAsync(Sprint sprint)
    {
        await db.SaveChangesAsync();
    }

    public async Task<string> GetNextIdAsync()
    {
        var re = new System.Text.RegularExpressions.Regex(@"^SPRINT-(\d+)$");
        ulong maxN = 0;
        var ids = await db.Sprints.Select(s => s.Id).ToListAsync();
        foreach (var id in ids)
        {
            var m = re.Match(id);
            if (m.Success && ulong.TryParse(m.Groups[1].Value, out var n) && n > maxN)
                maxN = n;
        }
        return $"SPRINT-{maxN + 1:D4}";
    }
}
