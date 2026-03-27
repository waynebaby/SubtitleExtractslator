# MCP Reference

## MCP-First Policy

1. Prefer MCP mode first; CLI is fallback.
2. MCP translation path is sampling-only.
3. If sampling fails (including missing server injection), return error directly.
4. External/custom endpoint access (for example `LLM_ENDPOINT`) is CLI route only.

## MCP Setup

1. Ask user whether to set up MCP in current workspace.
2. If agreed, create/update `./.vscode/mcp.json`.
3. On Windows, use absolute executable path for `servers.subtitle-extractslator.command`.
4. If config exists, merge/add server entry instead of overwriting unrelated servers.

Recommended Windows `mcp.json` snippet:

```json
{
  "servers": {
    "subtitle-extractslator": {
      "type": "stdio",
      "command": "E:\\code\\g\\SubtitleExtractslator\\.github\\skills\\subtitle-extractslator\\assets\\bin\\win-x64\\SubtitleExtractslator.Cli.exe",
      "args": ["--mode", "mcp"]
    }
  }
}
```

## MCP Tools

1. `probe`
2. `opensubtitles_search`
3. `extract`
4. `run_workflow`

`run_workflow_batch` is intentionally not exposed in MCP mode due to timeout risk in common MCP clients.

## Tool Return Contract

1. All tools return structured object with `ok`, `data`, and `error`.
2. Success: `ok=true`, `data` contains payload.
3. Failure: `ok=false`, `error` contains `code`, `message`, optional `snapshotPath`, and `timeUtc`.

## Runtime Notes

1. MCP server is stdio and can appear idle while waiting for frames.
2. Logging must not break stdio protocol.
3. Use MCP client tool responses for status and errors.
