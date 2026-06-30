using Microsoft.Extensions.Logging;

namespace SprintMcp.Application.Services;

internal static partial class TicketLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Ticket created: {TicketId} - {Title}")]
    public static partial void Created(ILogger logger, string ticketId, string title);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Ticket {TicketId} status changed to {NewStatus}")]
    public static partial void StatusChanged(ILogger logger, string ticketId, string newStatus);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Eval set for {TicketId}: run={RunId} verdict={Verdict}")]
    public static partial void EvalSet(ILogger logger, string ticketId, string runId, string verdict);
}
