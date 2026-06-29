namespace SprintMcp.Application.Abstractions;

public interface ISubagentRunChecker
{
    bool CheckRun(long epoch, string projectRoot);
}
