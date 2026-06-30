namespace SprintMcp.Application.Abstractions;

public interface ISubagentRunChecker
{
    Task<bool> CheckRunAsync(long epoch, string projectRoot, CancellationToken ct = default);
}
