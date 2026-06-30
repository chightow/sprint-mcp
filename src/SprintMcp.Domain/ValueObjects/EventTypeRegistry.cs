namespace SprintMcp.Domain.ValueObjects;

public static class EventTypeRegistry
{
    public static readonly IReadOnlySet<string> DomainTypes = new HashSet<string>
    {
        "TicketCreated",
        "TicketStatusChanged",
        "TicketPlanSet",
        "TicketPlanApproved",
        "CriterionAdded",
        "CriterionChecked",
        "DecisionRecorded",
        "TestPlanUpdated",
        "SummarySet",
        "EvalVerdictRecorded",
        "SprintStarted",
        "PhaseAdvanced",
        "SprintClosed",
        "SprintHandoffUpdated",
        "ActiveTaskAdded",
        "ActiveTaskRemoved",
    };

    public static readonly IReadOnlySet<string> AgentTypes = new HashSet<string>
    {
        "FileRead",
        "FileWrite",
        "EditString",
        "RunTerminal",
        "TerminalOutput",
        "GrepSearch",
        "FileSearch",
        "ToolResult",
        "Decision",
        "TaskComplete",
        "AskQuestions",
        "FetchWebpage",
    };

    public static readonly IReadOnlySet<string> ExecutionGatedTypes = new HashSet<string>
    {
        "FileWrite",
        "EditString",
        "RunTerminal",
    };

    public static bool IsDomain(string type) => DomainTypes.Contains(type);
    public static bool IsAgent(string type) => AgentTypes.Contains(type);
    public static bool IsKnown(string type) => IsDomain(type) || IsAgent(type);
    public static bool IsExecutionGated(string type) => ExecutionGatedTypes.Contains(type);
}
