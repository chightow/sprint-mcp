# sprint-mcp

.NET MCP stdio server for lightweight sprint/ticket management. SQLite storage, DDD architecture.

## Build & Run

```bash
dotnet run --project src/SprintMcp.Server
dotnet build SprintMcp.slnx
dotnet publish src/SprintMcp.Server -o ./publish
```

Storage: `.tickets/sprint.db` (SQLite via EF Core).

## Tools

### ticket

| Action | Description |
|---|---|
| `get` | Return ticket + nested arrays: `acceptance[]`, `decisions[]`, `test_plan[]`, `eval_report?` |
| `status` | Update ticket status (open, in_progress, closed, cancelled, archived) |
| `add_criterion` | Add acceptance criterion |
| `check_criterion` | Toggle satisfied on a criterion by id or ordinal |
| `set_plan` | Set tier/approach/files, optionally approve |
| `add_decision` | Insert structured decision |
| `add_test` | Add test plan item |
| `update_test` | Update test plan item status |
| `set_summary` | Set ticket summary prose |
| `set_eval` | Upsert evaluation report |

### sprint

| Action | Description |
|---|---|
| `start` | Create sprint row + ticket, no filesystem side-effects |
| `board` | List tickets in active sprint + handoff + active tasks |
| `close` | Validate eval reports, subagent-run match, stamp delivery receipt |

## Tests

```bash
dotnet test
```
