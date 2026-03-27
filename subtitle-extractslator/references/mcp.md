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
5. Always choose the binary path that matches current OS and CPU architecture.

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

Linux/macOS command path examples (set in `servers.subtitle-extractslator.command`):

1. Linux x64: `/path/to/subtitle-extractslator/assets/bin/linux-x64/SubtitleExtractslator.Cli`
1. Linux musl x64 (Alpine): `/path/to/subtitle-extractslator/assets/bin/linux-musl-x64/SubtitleExtractslator.Cli`
1. Linux ARM64: `/path/to/subtitle-extractslator/assets/bin/linux-arm64/SubtitleExtractslator.Cli`
1. Linux musl ARM64 (Alpine): `/path/to/subtitle-extractslator/assets/bin/linux-musl-arm64/SubtitleExtractslator.Cli`
1. Linux ARM (32-bit): `/path/to/subtitle-extractslator/assets/bin/linux-arm/SubtitleExtractslator.Cli`
1. macOS ARM64: `/path/to/subtitle-extractslator/assets/bin/osx-arm64/SubtitleExtractslator.Cli`
1. macOS x64: `/path/to/subtitle-extractslator/assets/bin/osx-x64/SubtitleExtractslator.Cli`

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
4. If user wants OpenSubtitles path but credentials are missing, ask user for `OPENSUBTITLES_API_KEY` first.
5. Ask whether user also wants to provide `OPENSUBTITLES_USERNAME` and `OPENSUBTITLES_PASSWORD`.
6. In MCP mode, do not commit secrets into repository files; prefer process/session env injection managed by MCP client.
