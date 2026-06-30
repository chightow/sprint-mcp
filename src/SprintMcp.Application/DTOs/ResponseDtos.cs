using System.Text.Json.Serialization;

namespace SprintMcp.Application.DTOs;

public sealed record TicketCreatedResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("title")] string Title
);

public sealed record TicketListResponse(
    [property: JsonPropertyName("tickets")] List<TicketSummaryDto> Tickets
);

public sealed record TicketSummaryDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("sprint_id")] string SprintId
);

public sealed record AcceptanceCriterionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("satisfied")] bool Satisfied
);

public sealed record DecisionDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("created_at")] string CreatedAt
);

public sealed record TestPlanItemDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("expected")] string Expected,
    [property: JsonPropertyName("status")] string Status
);

public sealed record EvalReportDto(
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("verdict")] string Verdict,
    [property: JsonPropertyName("matched_run_ts")] string MatchedRunTs
);

public sealed record TicketDetailResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("sprint_id")] string SprintId,
    [property: JsonPropertyName("plan_approach")] string PlanApproach,
    [property: JsonPropertyName("plan_files")] string PlanFiles,
    [property: JsonPropertyName("plan_approved_at")] string PlanApprovedAt,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("created_at")] string CreatedAt,
    [property: JsonPropertyName("updated_at")] string UpdatedAt,
    [property: JsonPropertyName("acceptance")] List<AcceptanceCriterionDto> Acceptance,
    [property: JsonPropertyName("decisions")] List<DecisionDto> Decisions,
    [property: JsonPropertyName("test_plan")] List<TestPlanItemDto> TestPlan,
    [property: JsonPropertyName("eval_report")] EvalReportDto? EvalReport
);

public sealed record TicketStatusResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("new_status")] string NewStatus
);

public sealed record CriterionAddedResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("criterion")] string Criterion,
    [property: JsonPropertyName("ordinal")] int Ordinal
);

public sealed record CriterionCheckedResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("criterion_id")] int CriterionId,
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("satisfied")] bool Satisfied
);

public sealed record PlanSetResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("plan_approved")] bool PlanApproved
);

public sealed record DecisionAddedResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("decision")] string Decision
);

public sealed record TestAddedResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("description")] string Description
);

public sealed record TestUpdatedResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("status")] string Status
);

public sealed record SummarySetResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId
);

public sealed record EvalSetResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("run_id")] string RunId,
    [property: JsonPropertyName("verdict")] string Verdict
);

public sealed record SprintStartedResponse(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("sprint_id")] string SprintId,
    [property: JsonPropertyName("message")] string Message
);

public sealed record HandoffDto(
    [property: JsonPropertyName("current_focus")] string CurrentFocus,
    [property: JsonPropertyName("in_progress")] string InProgress,
    [property: JsonPropertyName("discoveries")] string Discoveries,
    [property: JsonPropertyName("next_steps")] string NextSteps
);

public sealed record TicketBoardDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("plan_approved")] bool PlanApproved
);

public sealed record ActiveTaskDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("task_ref")] string TaskRef,
    [property: JsonPropertyName("ordinal")] int Ordinal
);

public sealed record SprintBoardResponse(
    [property: JsonPropertyName("sprint_id")] string SprintId,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("lock_held")] bool LockHeld,
    [property: JsonPropertyName("lock_held_since")] string LockHeldSince,
    [property: JsonPropertyName("tickets")] List<TicketBoardDto> Tickets,
    [property: JsonPropertyName("handoff")] HandoffDto? Handoff,
    [property: JsonPropertyName("active_tasks")] List<ActiveTaskDto> ActiveTasks
);

public sealed record SprintAdvancedResponse(
    [property: JsonPropertyName("sprint_id")] string SprintId,
    [property: JsonPropertyName("phase")] string Phase
);

public sealed record SprintReceiptItem(
    [property: JsonPropertyName("ticket_id")] string TicketId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("title")] string Title
);

public sealed record SprintClosedResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("sprint_id")] string SprintId,
    [property: JsonPropertyName("receipt")] List<SprintReceiptItem> Receipt
);

public sealed record HandoffUpdatedResponse(
    [property: JsonPropertyName("sprint_id")] string SprintId,
    [property: JsonPropertyName("current_focus")] string CurrentFocus,
    [property: JsonPropertyName("in_progress")] string InProgress,
    [property: JsonPropertyName("discoveries")] string Discoveries,
    [property: JsonPropertyName("next_steps")] string NextSteps
);

public sealed record TaskAddedResponse(
    [property: JsonPropertyName("sprint_id")] string SprintId,
    [property: JsonPropertyName("task_id")] int TaskId,
    [property: JsonPropertyName("task_ref")] string TaskRef
);

public sealed record TaskRemovedResponse(
    [property: JsonPropertyName("sprint_id")] string SprintId,
    [property: JsonPropertyName("removed_task_id")] int RemovedTaskId
);
