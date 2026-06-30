using SprintMcp.Application.Abstractions;

namespace SprintMcp.Server;

public class AgentContext(string agentId) : IAgentContext
{
    public string AgentId { get; } = agentId;
}
