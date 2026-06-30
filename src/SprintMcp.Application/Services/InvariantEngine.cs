using SprintMcp.Application.Abstractions;

namespace SprintMcp.Application.Services;

public class InvariantEngine
{
    private readonly IReadOnlyList<IInvariant> _invariants;
    private readonly IInvariantContext _context;

    public InvariantEngine(IEnumerable<IInvariant> invariants, IInvariantContext context)
    {
        _invariants = invariants.ToList();
        _context = context;
    }

    public async Task<CheckResult> CheckAsync(Domain.Entities.Event proposal, CancellationToken ct)
    {
        var applicable = _invariants.Where(i => i.AppliesTo(proposal)).ToList();
        if (applicable.Count == 0)
            return CheckResult.Pass();

        var failures = new List<string>();
        foreach (var inv in applicable)
        {
            var result = await inv.CheckAsync(proposal, _context, ct);
            if (!result.Valid && result.Failure is not null)
                failures.Add($"{inv.Name}: {result.Failure}");
        }

        return failures.Count > 0
            ? CheckResult.Fail(string.Join("; ", failures))
            : CheckResult.Pass();
    }
}
