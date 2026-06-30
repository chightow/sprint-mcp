# sprint-mcp — Agent Guide

.NET 10 MCP stdio server. DDD sprint/ticket mgmt. SQLite via EF Core. No prompt burden — tools enforce constraints.

## Commands

| Action | Command |
|--------|---------|
| Build | `dotnet build SprintMcp.slnx` |
| Run | `dotnet run --project src/SprintMcp.Server` |
| Publish | `dotnet publish src/SprintMcp.Server -o ./publish` |
| Test | `dotnet test` (65+ unit+integration, no external services) |
| DB path | Env `SPRINTMCP_DB_PATH` or `./.tickets/sprint.db` (auto-created) |

Required order: `dotnet build` before `dotnet test`. Single `.slnx`.

## Solution Map

```
SprintMcp.slnx
├── src/
│   ├── SprintMcp.Domain/       — Zero deps (no nuget)
│   ├── SprintMcp.Application/  — Depends on Domain
│   ├── SprintMcp.Infrastructure/ — Depends on Domain + Application
│   └── SprintMcp.Server/       — Depends on Application + Infrastructure
└── tests/
    └── SprintMcp.Tests/        — xUnit, Moq, in-memory SQLite
```

Entrypoint: `src/SprintMcp.Server/Program.cs`. MCP host wired as stdio server at startup. `DatabaseInitializer.InitializeAsync` runs on startup.

## Architecture Rules

### Non-negotiable

- Domain = zero framework/DB/HTTP deps
- External systems → port interfaces only, never direct
- Repos per entity (not just aggregate roots). No CRUD verbs in domain/repo method names
- Aggregate = static factory, no public ctor. No base class
- Domain events = immutable class, past-tense event type string, aggregate-raised, ubiquitous language
- Adapters translate protocol. No biz logic. No domain internals
- No domain/ORM model leak across adapter boundary → DTOs

### Per-layer

| Concept | Rule |
|---------|------|
| Entity | Unique ID. `Equals`/`GetHashCode` on ID only. Biz methods. No base class |
| VO | Immutable `record`. Equality on all fields. Validate at ctor. String-based typed IDs (`TicketId`, `SprintId`) with regex |
| Aggregate | Static factory. No public ctor. Reference other agg by ID. One agg per txn |
| Event | Immutable class. Past-tense event type string. Aggregate-raised. No vendor names. Primitives only |
| Repo (domain) | Interface in domain. Domain query methods. `Find` returns null. **Repo per entity** (not just aggregate roots) |
| Service | Orchestrate only. No biz logic. Stateless. Handle txn + events |
| Port (app) | Inbound → app. Outbound (infra) → app. Domain ports → domain |
| Adapter | Implements port. Handle errors → `ToolResult.Error`. No biz logic |
| DI | Compose at startup in `DependencyInjection.cs` per project. One place swap impl |

### Naming

| What | Pattern | Example |
|------|---------|---------|
| Domain port | `I{Concept}Repository/Service` | `ISprintRepository`, `IEventStore` |
| App infra port | `I{Concept}Port` | `IAgentContext`, `ISubagentRunChecker` |
| Adapter | `{Concept}Repository` (no tech prefix) | `SprintRepository`, `TicketRepository` |
| Service | `{Verb}{Noun}Service` | `TicketService`, `SprintService`, `EventService` |
| Event | `{Aggregate}{PastTense}` | `SprintStarted`, `TicketCreated` |
| MCP tool | `{noun}_{action}` | `ticket_create`, `sprint_close` |

### VO pattern

All VOs follow: public ctor (validation in ctor), `FromString` factory, `Value` property, `ToString` override, `Validate` static. String-based records with regex validation:
- `TicketId`: `^TKT-\d{4,}$`
- `SprintId`: `^SPRINT-\d{4,}$`

Status/phase VOs use static instances + precomputed transition matrix (`CanTransitionTo`).

## Sprint Phase Lifecycle

```
planning → executing → evaluating → complete (or failed)
```

Phase gates enforced at service layer. Each phase locks specific tool actions (see README table).

## Event Protocol

Two event categories stored in `Events` table:
- **domain**: 16 types, system-emitted. Agents cannot propose these
- **agent**: 12 types, proposed via `propose_event` tool. Validated through `InvariantEngine`
- **Execution-gated**: `FileWrite`, `EditString`, `RunTerminal` — only during `executing` phase

`cause_by` string array for causal attribution. `propose_event` validates + appends. `list_events` cursor-based pagination.

## Invariant Engine

Pluggable `IInvariant` rules run on every agent event proposal:
- **PhaseGateInvariant**: Rejects write events outside executing phase
- **TicketStatusTransitionInvariant**: Validates status transition matches current DB state

## Server Layer (Handlers)

MCP tools defined as `[McpServerToolType]` classes in `src/SprintMcp.Server/Handlers/`. Each handler wraps a service call with try/catch → `result.ToMcpResult()`.

Three handler classes: `TicketHandler`, `SprintHandler`, `EventToolHandler`.

## Test Patterns

- xUnit with `IAsyncLifetime` for DB setup/teardown
- In-memory SQLite per test class (`SqliteConnection "Data Source=:memory:"`)
- `DatabaseInitializer.InitializeAsync` in `InitializeAsync`
- `SetupSprintAsync` helper for phase setup
- `EventTestHelpers.CreateEventDeps` for event store + invariant engine wiring
- `Mock.Of<ILogger<T>>` for loggers
- Moq for external ports (`ISubagentRunChecker`)
- Tests use real EF Core + repo implementations, not mocked repos — services call repo interfaces directly
- Each test creates fresh `AppDbContext` + service instance

## Field Limits

Checked at service layer before DB. Defined in `FieldLimits.cs`. See README table for all max values.

## Idempotency

Mutating ticket actions accept optional `idempotency_key` (24h TTL). Implemented via `IdempotencyService` + `IdempotencyKeys` table.

## Locks

- `TicketLock`: global semaphore for ticket mutations
- `SprintLock`: per-sprint semaphore, visible via `board` lock diagnostics

## Tool Result Pattern

All services return `ToolResult` (Status: "ok"/"error", Data, Message, EventId). Handlers serialize via `ToMcpResult()`. DTOs in `ResponseDtos.cs` — all sealed records with `[JsonPropertyName]` snake_case.
