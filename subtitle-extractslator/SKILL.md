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

This skill orchestrates subtitle discovery and translation using MCP-first execution with local CLI fallback.

Primary goals:
1. Keep timeline and subtitle structure stable.
2. Prioritize existing subtitle resources before extraction.
3. Use grouped rolling context for better semantic consistency.

## MCP-First Setup

Default policy (important):
1. Prefer MCP mode first. CLI mode is fallback.
2. MCP mode supports official sampling (`sampling/createMessage`) when client capabilities are available.
3. Before running probe/search/extract/workflow, first ask user whether to set up MCP in current workspace.
4. If user agrees, create or update workspace MCP config at `./.vscode/mcp.json`.
5. If `./.vscode/mcp.json` already exists, merge/add `subtitle-extractslator` server entry instead of overwriting unrelated servers.
6. If user declines, continue with CLI commands and explicitly state MCP setup was skipped.

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

Platform binary paths inside this skill:
1. Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe`
2. Linux: `./assets/bin/linux-x64/SubtitleExtractslator.Cli`
3. macOS (Apple Silicon): `./assets/bin/osx-arm64/SubtitleExtractslator.Cli`

Quick check (`--help`):
1. Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --help`
2. Linux: `./assets/bin/linux-x64/SubtitleExtractslator.Cli --help`
3. macOS: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli --help`

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

MCP tool names:
1. probe
2. opensubtitles_search
3. extract
4. run_workflow

## Workflow Contract

Follow this exact order:

0. MCP setup prompt first.
Ask user whether to configure MCP for current workspace now.
If yes, create or update `./.vscode/mcp.json` and confirm `subtitle-extractslator` server is available.
If no, continue in CLI mode.
1. Confirm user target: input file and target language.
2. Probe internal subtitle tracks.
If target language exists, report and stop.
3. Query OpenSubtitles candidates.
If user mention not using open subtitles or no candidates, continue next.
If candidates exist,  adopt the most popular candidate. download, rename to output path.
4. Extract local subtitle.
Prefer English track.
If English not present, pick deterministic nearest-language fallback.
5. Build timeline cue objects and split into groups.
Rule A: if current stack is non-empty and no dialogue for one minute, start new group.
Rule B: if group exceeds 100 cues, start new group.
6. Rolling context update.
For each group, generate scene summary and update historical core knowledge state.
7. Translate each group using scene summary plus historical knowledge plus group timeline objects.
Preserve index, timestamps, line counts, and segmentation rhythm.
8. Merge all translated groups into final SRT.

## Mode-Aware Translation Source Policy

1. MCP mode (preferred):
Sampling first, external provider fallback.
When the MCP server instance cannot be injected during `run_workflow`, skip sampling, log the reason, and go directly to external provider.
2. Non-MCP mode (fallback):
External provider only.

## Guardrails

1. Never modify timestamps or cue ordering.
2. Never merge or split cues unless explicit user request overrides default policy.
3. If structure validation fails, report error and stop instead of emitting broken output.
4. If OpenSubtitles candidate exists, always ask user before adoption.

## Operational Notes

1. Prefer deterministic behavior over creative rewriting.
2. Keep translation natural and context aware, but preserve subtitle pacing.
3. For command details and troubleshooting, read `references/commands.md` and `references/troubleshooting.md`.
4. For literary or entertainment subtitles (jokes, sarcasm, taboo language, sexual humor, dark comedy), strongly prefer an uncensored model variant. Censored models are more likely to weaken punchlines, skip sensitive phrasing, or leave source fragments untranslated.
5. In agent scenarios, prefer MCP mode with explicit plan steps. Reusing MCP sampling through the existing client session can reduce token spend and lower deployment/ops overhead compared with standing up separate external-only translation services.
