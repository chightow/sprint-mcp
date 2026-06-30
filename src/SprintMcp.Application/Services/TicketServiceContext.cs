using Microsoft.Extensions.Logging;
using SprintMcp.Application.Abstractions;
using SprintMcp.Domain.Repositories;

namespace SprintMcp.Application.Services;

public sealed record TicketServiceContext(
    ITicketRepository TicketRepo,
    IAcceptanceCriterionRepository CriterionRepo,
    IDecisionRepository DecisionRepo,
    ITestPlanItemRepository TestPlanRepo,
    IEvalReportRepository EvalReportRepo,
    ISprintRepository SprintRepo,
    ISubagentRunChecker RunChecker,
    IdempotencyService Idempotency,
    IEventStore EventStore,
    InvariantEngine InvariantEngine,
    string ProjectRoot,
    TimeProvider TimeProvider,
    ITicketLock TicketLock,
    ILogger<TicketService> Logger
);
