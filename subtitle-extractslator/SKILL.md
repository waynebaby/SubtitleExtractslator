---
name: subtitle-extractslator
description: Probe media subtitle tracks, search OpenSubtitles candidates, extract fallback subtitles, and run grouped context-aware translation while preserving SRT timing and structure. Use when user asks to find subtitle language, reuse existing subtitle tracks, check OpenSubtitles, or produce translated subtitle files with stable rhythm and timeline.
compatibility: Designed for Claude Code environments with local executable access and FFmpeg available.
license: MIT
metadata:
  author: waynebaby
  version: 0.1.1
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

## Trigger Guidance

Use this skill when user asks to:
1. Check whether a media file already has a specific subtitle language.
2. Search online subtitle candidates before local extraction.
3. Translate subtitles while preserving SRT timing and segmentation rhythm.
4. Produce a final SRT file from a media file or existing subtitle file.

## Reference Map

Read these reference files for operational details:
1. `references/cli.md`:
- runtime paths and binaries
- supported platform package matrix (win-x64/win-arm64/linux-x64/linux-musl-x64/linux-arm64/linux-musl-arm64/linux-arm/osx-arm64/osx-x64)
- CLI command examples and batch mode
- output path policy
2. `references/mcp.md`:
- MCP-first setup and `mcp.json` rules
- exposed tools and return contract
- MCP runtime notes and constraints
3. `references/commands.md`:
- complete command and environment variable matrix
4. `references/troubleshooting.md`:
- failure patterns and diagnostics checklist

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

OpenSubtitles credential interaction rule (must execute before real OpenSubtitles call):
1. If `OPENSUBTITLES_API_KEY` is missing in current context/environment, ask user for it.
2. Ask whether user also wants authenticated download reliability via username/password.
3. If user provides username, then ask password in the next prompt.
4. Do not persist secrets into repository files by default.
5. Apply provided values as temporary runtime env overrides for current command/workflow.
6. If user refuses to provide credentials, skip OpenSubtitles branch and continue local extraction fallback.

Credential Q&A prompts (required wording style):
1. `Please provide OpenSubtitles API key (OPENSUBTITLES_API_KEY) for this run.`
2. `Do you want to provide OpenSubtitles username/password for authenticated download as well?`
3. `Please provide OpenSubtitles username (OPENSUBTITLES_USERNAME).`
4. `Please provide OpenSubtitles password (OPENSUBTITLES_PASSWORD).`

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


