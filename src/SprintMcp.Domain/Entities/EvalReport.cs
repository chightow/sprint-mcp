using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class EvalReport
{
    public string TicketId { get; private set; } = string.Empty;
    public string RunId { get; private set; } = string.Empty;
    public Verdict Verdict { get; private set; } = Verdict.Pending;
    public string Content { get; private set; } = string.Empty;
    public DateTime? MatchedRunTs { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private EvalReport() { }

    public EvalReport(string ticketId, string runId, Verdict verdict, string content, DateTime? matchedRunTs = null, DateTime? updatedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId, nameof(ticketId));
        ArgumentException.ThrowIfNullOrWhiteSpace(runId, nameof(runId));
        TicketId = ticketId;
        RunId = runId;
        Verdict = verdict;
        Content = content ?? string.Empty;
        MatchedRunTs = matchedRunTs;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = updatedAt ?? DateTime.UtcNow;
    }

    public void Update(string runId, Verdict verdict, string content, DateTime updatedAt, DateTime? matchedRunTs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId, nameof(runId));
        RunId = runId;
        Verdict = verdict;
        Content = content ?? string.Empty;
        MatchedRunTs = matchedRunTs;
        UpdatedAt = updatedAt;
    }

    public void MarkRunMatched(DateTime matchedRunTs, DateTime updatedAt)
    {
        MatchedRunTs = matchedRunTs;
        UpdatedAt = updatedAt;
    }
}
