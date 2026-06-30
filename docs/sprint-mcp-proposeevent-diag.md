# sprint-mcp `propose_event` invocation failure

## Symptom

Every `propose_event` tool call returns `"An error occurred invoking 'propose_event'."`

This is a **generic MCP SDK error** — it fires BEFORE the handler's try-catch in
`EventHandler.cs:19-27`. sprint-mcp's catch block is never reached.

`sprint-do` side: error caught at `ApiAgentService.cs:462` and logged as debug.

Non-blocking — the sprint runs fine without event proposals.

## What was tried

| Fix | Result |
|-----|--------|
| Route console logs to stderr (e5c8dba) | No effect on this error |
| MCP initialize handshake in `McpClient` | No effect |
| Remove `proposed_by` / `signature` from args | No effect |
| Swap `caused_by` from `string[]?` to `string` (620a0f3) | No effect |
| Omit `caused_by` entirely | No effect |

## Payload sprint-do sends

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "propose_event",
    "arguments": {
      "event_type": "FileWrite",
      "aggregate_id": "TKT-001",
      "event_data": "{\"tool\":\"write_file\",\"args\":\"{\\\"filePath\\\":\\\"hello.py\\\"}\",\"isWrite\":false}"
    }
  }
}
```

Note: `event_data` is a JSON string containing nested escaped JSON. It gets
triple-escaped through JSON-RPC serialization (`\"` → `\\\"`). The MCP SDK must
deserialize this back to a plain `string` parameter.

## Other tools work

`list_events` (same handler class, same MCP SDK path) works correctly:
- `long? since`, `string? type`, `string? aggregate_id`, `int? take`
- No deeply-nested-JSON-string parameters

## Suspected root cause

MCP SDK v1.4.0 fails to deserialize `event_data` as a `string` parameter when
the value is a deeply nested escaped JSON string. The SDK likely tries to infer
the type from the JSON value rather than treating it as a literal string.

The triple-escaped JSON string (`\"` → `\\\"`) triggers a deserialization path
that doesn't expect a string containing escaped quotes.

## Fix

Changed `event_data` from `string` to `System.Text.Json.JsonElement`. The MCP
SDK properly deserializes a JSON value into `JsonElement`, then we serialize to
string internally using `JsonSerializer.Serialize()`.
