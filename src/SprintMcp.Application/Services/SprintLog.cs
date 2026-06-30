using Microsoft.Extensions.Logging;

namespace SprintMcp.Application.Services;

internal static partial class SprintLog
{
    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Sprint {SprintId} closed with {TicketCount} tickets")]
    public static partial void Closed(ILogger logger, string sprintId, int ticketCount);
}
