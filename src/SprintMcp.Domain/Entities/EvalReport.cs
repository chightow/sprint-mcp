using SprintMcp.Domain.ValueObjects;

namespace SprintMcp.Domain.Entities;

public class EvalReport
{
    public string TicketId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public Verdict Verdict { get; set; } = Verdict.Pending;
    public string Content { get; set; } = string.Empty;
    public string? MatchedRunTs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
