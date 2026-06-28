# sprint-mcp

MCP stdio server for sprint and ticket management. Lightweight replacement for the canon ticket filesystem ceremony — the agent talks to this over JSON-RPC instead of parsing markdown.

## Tools

### ticket
- `get` — read ticket + all docs
- `status` — update ticket status
- `doc` — read/write companion docs
- `add_criterion` — add acceptance criterion

### sprint
- `start` — create ticket + plan + ACTIVE
- `board` — list all tickets + handoff
- `close` — validate eval-report.md, subagent-run match, pass verdict

## Build

```bash
go build -o sprint-mcp ./cmd/mcp-server/
sprint-mcp                    # stdio MCP protocol
```

## Origin

Extracted from [canon](https://github.com/sunitghub/canon-skills) by Sunit Joshi (MIT).
