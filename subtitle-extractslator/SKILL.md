---
name: subtitle-extractslator
description: Probe media subtitle tracks, search OpenSubtitles candidates, extract fallback subtitles, and run grouped context-aware translation while preserving SRT timing and structure. Use when user asks to find subtitle language, reuse existing subtitle tracks, check OpenSubtitles, or produce translated subtitle files with stable rhythm and timeline.
compatibility: Designed for Claude Code environments with local executable access and FFmpeg available.
license: MIT
metadata:
  author: waynebaby
  version: 0.1.0
  mcp-server: subtitle-extractslator
  category: subtitle-translation
  language: zh-CN
---

# Subtitle Extractslator Skill

## Purpose

This repository is skill-first: the subtitle skill package is the primary deliverable, while CLI and MCP are runtime layers used to execute the skill.

This skill orchestrates subtitle discovery and translation using MCP-first execution with local CLI fallback.

Primary goals:
1. Keep timeline and subtitle structure stable.
2. Prioritize existing subtitle resources before extraction.
3. Use grouped rolling context for better semantic consistency.
4. Keep skill behavior consistent across agent (MCP) and script (CLI) execution paths.

## MCP-First Setup

Default policy (important):
1. Prefer MCP mode first. CLI mode is fallback.
2. MCP mode supports official sampling (`sampling/createMessage`) when client capabilities are available.
3. MCP translation path is sampling-only. If sampling fails (including missing MCP server injection), return error directly.
4. Custom external endpoint access (for example `LLM_ENDPOINT` and related auth/key config) belongs to CLI route only.
5. Before running probe/search/extract/workflow, first ask user whether to set up MCP in current workspace.
6. If user agrees, create or update workspace MCP config at `./.vscode/mcp.json`.
7. On Windows, set `servers.subtitle-extractslator.command` to an absolute executable path (do not use relative `./.github/...` in MCP config).
8. If `./.vscode/mcp.json` already exists, merge/add `subtitle-extractslator` server entry instead of overwriting unrelated servers.
9. If user declines, continue with CLI commands and explicitly state MCP setup was skipped.

Recommended Windows workspace MCP config example:
```json
{
  "servers": {
    "subtitle-extractslator": {
      "type": "stdio",
      "command": "E:\\code\\g\\SubtitleExtractslator\\.github\\skills\\subtitle-extractslator\\assets\\bin\\win-x64\\SubtitleExtractslator.Cli.exe",
      "args": [
        "--mode",
        "mcp"
      ]
    }
  }
}
```

## Trigger Guidance

Use this skill when user asks to:
1. Check whether a media file already has a specific subtitle language.
2. Search online subtitle candidates before local extraction.
3. Translate subtitles while preserving SRT timing and segmentation rhythm.
4. Produce a final SRT file from a media file or existing subtitle file.

## Runtime Paths

Execution path rule (important):
1. Paths below are deterministic and relative to the skill root folder.
2. Do not scan the whole disk to locate the binary.
3. If current working directory is repository root, prepend `./.github/skills/subtitle-extractslator/`.
4. Always quote file paths that contain spaces (for example `--input "Z:\\My Folder\\movie.mp4"`). This provides best cross-shell compatibility.

Platform binary paths inside this skill:
1. Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe`
2. Linux: `./assets/bin/linux-x64/SubtitleExtractslator.Cli`
3. macOS (Apple Silicon): `./assets/bin/osx-arm64/SubtitleExtractslator.Cli`

Quick check (`--help`):
1. Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --help`
2. Linux: `./assets/bin/linux-x64/SubtitleExtractslator.Cli --help`
3. macOS: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli --help`

CLI global options:
1. `--env "KEY=VALUE;KEY2=VALUE2"`: temporary environment overrides for current command.
2. `--help`: print complete CLI command help.

Output path policy (critical):
1. Final subtitle output must be written to the same folder as the input video (or input subtitle) unless user explicitly requests another folder.
2. For `run-workflow`, always pass explicit `--output` path in that input folder.
3. If user does not provide output filename, default to `<input_basename>.<lang>.srt` in input folder.
4. Do not place final outputs in random or temporary directories. Temporary files are allowed only for internal intermediate steps.

CLI mode examples (Windows / Linux / macOS):
1. Probe:
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli probe --input "movie.mkv" --lang zh`
- Linux: `./assets/bin/linux-x64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`
- macOS: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`
2. Search OpenSubtitles candidates:
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli opensubtitles-search --input "movie.mkv" --lang zh`
- Linux: `./assets/bin/linux-x64/SubtitleExtractslator.Cli --mode cli opensubtitles-search --input "movie.mkv" --lang zh`
- macOS: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli --mode cli opensubtitles-search --input "movie.mkv" --lang zh`
3. Full workflow:
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt"`
- Linux: `./assets/bin/linux-x64/SubtitleExtractslator.Cli --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt"`
- macOS: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt"`
4. Batch workflow (CLI only):
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli run-workflow-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"`
- Linux: `./assets/bin/linux-x64/SubtitleExtractslator.Cli --mode cli run-workflow-batch --input-list "./inputs.txt" --lang zh --output-dir "./out" --output-suffix ".zh.srt"`
- macOS: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli --mode cli run-workflow-batch --input-list "./inputs.txt" --lang zh --output-dir "./out" --output-suffix ".zh.srt"`

Batch input list rules:
1. UTF-8 text file.
2. One input path per line.
3. Empty lines and lines starting with `#` are ignored.
4. Output naming defaults to `.<lang>.srt` when `--output-suffix` is not provided.

MCP tool names:
1. probe
2. opensubtitles_search
3. extract
4. run_workflow

MCP tool return contract:
1. All MCP tools return a structured object with `ok`, `data`, and `error`.
2. Success: `ok=true`, `data` contains the tool payload.
3. Failure: `ok=false`, `error` contains `code`, `message`, optional `snapshotPath`, and `timeUtc`.

MCP note:
1. `run_workflow_batch` is intentionally not exposed in MCP mode because long-running batch calls are prone to timeout in common MCP clients.

## Workflow Contract

Follow this exact decision tree (do not reorder):

0. MCP setup prompt first.
Ask user whether to configure MCP for current workspace now.
If yes, create or update `./.vscode/mcp.json`; on Windows use absolute `command` path for `subtitle-extractslator` server and confirm server is available.
If no, continue in CLI mode.

1. Confirm user target:
- input file
- target language
- deterministic output path
Output path rule: if user did not provide output filename, use `<input_basename>.<lang>.srt` in the same folder as input.

2. Probe internal subtitle tracks.
IF target language already exists in embedded tracks:
- report and stop.
ELSE continue.

3. Select source subtitle material in this strict order:
1. Embedded subtitle tracks from input video:
- prefer `en/eng`
- if no `en/eng`, use another available embedded subtitle language
2. IF no embedded subtitle tracks exist:
- search local input folder and all subfolders for `*.srt`
- prefer English file when available, otherwise use another available language file
3. IF still no local subtitle material:
- search OpenSubtitles in any language
- prefer English candidates first
- then use the best available non-English candidate

4. Build timeline cue objects and split into groups.
Default implementation groups by fixed cue count (`cuesPerGroup`, default 5, overridable by CLI/env).
Then merge group bodies by translation body size (`bodySize`, default 20, overridable by CLI/env).

5. Rolling context update.
For each group, generate scene summary and update historical core knowledge state.

6. Translate each group using scene summary plus historical knowledge plus group timeline objects.
Preserve index, timestamps, line counts, and segmentation rhythm.

7. Merge all translated groups into final SRT.

## Mode-Aware Translation Source Policy

1. MCP mode (preferred):
Sampling only.
When sampling fails (including missing MCP server instance injection during `run_workflow`), return error directly.
MCP sampling uses retry policy consistent with LLM retry settings (`LLM_RETRY_COUNT` / override parameter). When oversized responses are detected, the next retry injects a concise-reasoning warning to reduce overthinking output.
2. Non-MCP mode (fallback):
External provider only.
All custom external endpoint access is CLI route responsibility.

## Guardrails

1. Never modify timestamps or cue ordering.
2. Never merge or split cues unless explicit user request overrides default policy.
3. If structure validation fails, report error and stop instead of emitting broken output.
4. Embedded subtitle source selection is deterministic: `en/eng` first, otherwise first available language.
5. When no embedded subtitle exists, local folder/subfolder subtitle search is attempted before OpenSubtitles.
6. OpenSubtitles fallback uses language preference `en/eng` first, then other available languages.

## Operational Notes

1. Prefer deterministic behavior over creative rewriting.
2. Keep translation natural and context aware, but preserve subtitle pacing.
3. For command details and troubleshooting, read `references/commands.md` and `references/troubleshooting.md`.
4. For literary or entertainment subtitles (jokes, sarcasm, taboo language, sexual humor, dark comedy), strongly prefer an uncensored model variant. Censored models are more likely to weaken punchlines, skip sensitive phrasing, or leave source fragments untranslated.
5. In agent scenarios, prefer MCP mode with explicit plan steps. Reusing MCP sampling through the existing client session can reduce token spend and lower deployment/ops overhead compared with standing up separate external-only translation services.
