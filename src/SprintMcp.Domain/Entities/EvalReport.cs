using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class EvalReport
{
    public string TicketId { get; private set; } = string.Empty;
    public string RunId { get; private set; } = string.Empty;
    public Verdict Verdict { get; private set; } = Verdict.Pending;
    public string Content { get; private set; } = string.Empty;
    public string? MatchedRunTs { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    private EvalReport() { }

    public EvalReport(string ticketId, string runId, Verdict verdict, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId, nameof(ticketId));
        ArgumentException.ThrowIfNullOrWhiteSpace(runId, nameof(runId));
        _ = Verdict.FromString(verdict.Value);
        TicketId = ticketId;
        RunId = runId;
        Verdict = verdict;
        Content = content ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(string runId, Verdict verdict, string content, DateTime updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId, nameof(runId));
        _ = Verdict.FromString(verdict.Value);
        RunId = runId;
        Verdict = verdict;
        Content = content ?? string.Empty;
        UpdatedAt = updatedAt;
    }
}
