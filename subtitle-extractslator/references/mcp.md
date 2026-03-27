# MCP Reference

## MCP-First Policy

1. Prefer MCP mode first; CLI is fallback.
2. MCP translation path is sampling-only.
3. If sampling fails (including missing server injection), return error directly.
4. External/custom endpoint access (for example `LLM_ENDPOINT`) is CLI route only.

## MCP Setup

1. Ask user whether to set up MCP in current workspace.
2. If agreed, create/update the MCP config file for the active agent client (do not assume one fixed file path).
3. On all platforms, use absolute executable path for `servers.subtitle-extractslator.command`.
4. If config exists, merge/add server entry instead of overwriting unrelated servers.
5. Always choose the binary path that matches current OS and CPU architecture.

Agent MCP config target rule:

1. GitHub Copilot: usually `./.vscode/mcp.json`.
2. Claude Code/OpenClaw/Codex: use each client's MCP config location for current workspace/profile.
3. When client-specific path is unknown, ask user for the exact target config file before writing.

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

Cross-platform command path rule:

1. Set `servers.subtitle-extractslator.command` to absolute binary path matching current RID under the active skill directory (`.github/skills` or `.claude/skills`) at `subtitle-extractslator/assets/bin/<rid>/`.
2. Windows executable name: `SubtitleExtractslator.Cli.exe`; Linux/macOS executable name: `SubtitleExtractslator.Cli`.
3. Linux/macOS command example pattern: `/path/to/<agent_workspace>/.github/skills/subtitle-extractslator/assets/bin/<rid>/SubtitleExtractslator.Cli` (or use `.claude/skills` if that is your agent workspace skill path).

## MCP Tools

1. `probe`
2. `opensubtitles_search`
3. `opensubtitles_download`
4. `extract`
5. `run_workflow`

`opensubtitles_download` behavior:
1. Requires `fileId` from a previous `opensubtitles_search` candidate.
2. Does not run search internally and does not support `candidateRank`.
3. Requires explicit `opensubtitlesApiKey` parameter for real API access.

OpenSubtitles parameter contract:
1. `opensubtitles_search` requires `opensubtitlesApiKey`.
2. `opensubtitles_download` requires `opensubtitlesApiKey`.
3. Optional for both tools: `opensubtitlesUsername`, `opensubtitlesPassword`, `opensubtitlesEndpoint`, `opensubtitlesUserAgent`.
4. `run_workflow` accepts optional OpenSubtitles parameters; without them, OpenSubtitles branch is skipped.

`run_workflow_batch` is intentionally not exposed in MCP mode due to timeout risk in common MCP clients.

## Tool Return Contract

1. All tools return structured object with `ok`, `data`, and `error`.
2. Success: `ok=true`, `data` contains payload.
3. Failure: `ok=false`, `error` contains `code`, `message`, optional `snapshotPath`, and `timeUtc`.

## Runtime Notes

1. MCP server is stdio and can appear idle while waiting for frames.
2. Logging must not break stdio protocol.
3. Use MCP client tool responses for status and errors.
4. OpenSubtitles credentials are passed as explicit tool parameters, not process environment variables.
5. In MCP mode, do not commit secrets into repository files.
