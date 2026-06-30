# sprint-mcp

.NET MCP stdio server for lightweight sprint/ticket management. SQLite storage, DDD architecture.

**No prompt burden.** Tools enforce all safety — the LLM is never told "be careful" or "check phase."

## Build & Run

```bash
dotnet run --project src/SprintMcp.Server
dotnet build SprintMcp.slnx
dotnet publish src/SprintMcp.Server -o ./publish
```

Storage: `.tickets/sprint.db` (SQLite via EF Core). Auto-created on first run.

## Sprint Phase Lifecycle

```
planning ──[advance_phase]──► executing ──[advance_phase]──► evaluating ──[close]──► complete
```

Every mutation tool checks the current sprint phase. Wrong phase → error returned immediately.

| Tool action | Required phase |
|---|---|
| `ticket create` | planning |
| `ticket set_plan` | planning |
| `ticket add_criterion` | planning |
| `ticket add_decision` | planning |
| `ticket add_test` | planning |
| `ticket status` | executing |
| `ticket check_criterion` | executing |
| `ticket update_test` | executing |
| `ticket set_summary` | evaluating |
| `ticket set_eval` | evaluating |
| `sprint close` | evaluating |
| `sprint update_handoff` | any active phase |
| `sprint add_task` / `remove_task` | any active phase |

## Status Transition Matrix

Defined as adjacency dictionaries on value objects:

```
Ticket:    open → in_progress → closed/cancelled → archived → (frozen)
Sprint:    active → closed
SprintPhase: planning → executing → evaluating → complete/failed
```

Illegal transitions are rejected at the service layer — no prompt burden.

## Field Limits

Rejected at the **service layer** before reaching the DB:

| Field | Max |
|---|---|
| `title` | 200 |
| `description` | 2000 |
| `criterion.text` | 500 |
| `decision.title` | 200 |
| `decision.rationale` | 2000 |
| `test_plan.description` | 500 |
| `test_plan.expected` | 500 |
| `ticket.summary` | 5000 |
| `eval_report.content` | 10000 |
| `handoff.*` | 2000 |
| `task_ref` | 200 |

## Tools

### ticket

Single tool, dispatches on `action`. Read actions (`get`, `list`) are phase-agnostic.

| Action | Description |
|---|---|
| `create` | Create ticket. Checks phase=planning, max 50 per sprint. |
| `get` | Return ticket + nested arrays: `acceptance[]`, `decisions[]`, `test_plan[]`, `eval_report?` |
| `list` | All tickets across all sprints |
| `status` | Update ticket status (enforces transition matrix) |
| `add_criterion` | Add acceptance criterion |
| `check_criterion` | Toggle satisfied on a criterion by id or ordinal |
| `set_plan` | Set tier/approach/files, optionally approve |
| `add_decision` | Insert structured decision |
| `add_test` | Add test plan item |
| `update_test` | Update test plan item status |
| `set_summary` | Set ticket summary prose |
| `set_eval` | Upsert evaluation report. Validates subagent run-id immediately. |

Mutating actions accept optional `idempotency_key` (24h TTL) for safe LLM retries.

### sprint

| Action | Description |
|---|---|
| `start` | Create sprint (planning phase) + first ticket |
| `board` | Sprint manifest: tickets, handoff, active tasks, phase, lock state |
| `advance_phase` | planning→executing→evaluating. One step per call. |
| `close` | Requires evaluating phase. Validates eval reports, subagent-run match, stamps receipt. |
| `update_handoff` | Upsert current_focus / in_progress / discoveries / next_steps |
| `add_task` | Add active task by task_ref |
| `remove_task` | Remove active task by id |

`board` includes lock diagnostics: `lock_held` (bool), `lock_held_since` (ISO timestamp).

## Subagent Run Validation

- `set_eval` validates the run-id against `{projectRoot}/.canon|.claude|.opencode/subagent-runs.jsonl` **immediately** — not at close time.
- `close` stamps `MatchedRunTs` on each eval report after a final check.

## Tests

```bash
dotnet test
```

61 tests: unit + integration with in-memory SQLite per test class.
