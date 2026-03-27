---
name: subtitle-extractslator
description: Probe media subtitle tracks, search OpenSubtitles candidates, extract fallback subtitles, and run grouped context-aware translation while preserving SRT timing and structure. Use when user asks to find subtitle language, reuse existing subtitle tracks, check OpenSubtitles, or produce translated subtitle files with stable rhythm and timeline.
compatibility: Designed for agent environments (GitHub Copilot, Claude Code, OpenClaw, Codex) with local executable access and FFmpeg available.
license: MIT
metadata:
  author: waynebaby
  version: 0.1.2
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
3. `references/opensubtitles.md`:
- OpenSubtitles explicit-parameter credential contract (CLI + MCP)
- search/download fallback strategy, rate-limit handling, and parameter matrix
4. `references/commands.md`:
- complete command and environment variable matrix
5. `references/troubleshooting.md`:
- failure patterns and diagnostics checklist

## Workflow Contract

Follow this exact decision tree (do not reorder):

0. MCP setup prompt first.
Ask user whether to configure MCP for current workspace now.
If yes, create or update the MCP config file for the current agent client (for example, GitHub Copilot commonly uses `./.vscode/mcp.json`; Claude Code/OpenClaw/Codex use their own MCP config locations); on all platforms use absolute `command` path for `subtitle-extractslator` server and confirm server is available.
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
- OpenSubtitles dual-query and fallback strategy (CRITICAL, mandatory for every OpenSubtitles search):
- every `opensubtitles_search` call must pass two query parameters together:
  - primary query: current video title/base filename
  - normalized query: normalized episode-style keyword from full path, for example `<series_or_title> s00e00`
- fallback execution is internal-only: MCP/CLI C# code must run primary-first then normalized retry; skill layer must not split this into parallel or separate fallback jobs
- if both queries return no candidate, report not found and continue local extraction fallback

OpenSubtitles credential and download rule (CLI + MCP):
1. OpenSubtitles real API calls must use explicit function/command parameters, not process environment variables.
2. Required credential parameter name is `opensubtitlesApiKey` (CLI flag: `--opensubtitles-api-key`).
3. Optional parameters: `opensubtitlesUsername`, `opensubtitlesPassword`, `opensubtitlesEndpoint`, `opensubtitlesUserAgent`.
4. CLI download supports both direct `fileId` and ranked candidate selection (`candidateRank`, default `1`).
5. MCP `opensubtitles_download` is download-only: it requires `fileId` from prior `opensubtitles_search` result and must not trigger internal search.
6. CLI ranked download must reuse the same CRITICAL fallback-aware search strategy.
7. If user refuses to provide required credential parameters, skip OpenSubtitles branch and continue local extraction fallback.
8. Detailed parameter matrix and examples are maintained in `references/opensubtitles.md`.
9. Skill-level orchestration is strictly linear: `opensubtitles_search` and `opensubtitles_download` must run one-by-one; parallel execution is forbidden.

OpenSubtitles rate-limit handling (CRITICAL):
1. If API response indicates rate limit (for example HTTP `429` or explicit `rate limit exceeded`), disable OpenSubtitles parallel requests immediately.
2. After rate limit is detected, process OpenSubtitles requests strictly one-by-one (serial only).
3. Insert delay between each request in serial mode before sending the next one.
4. Retry per request up to 20 times when rate-limited, and increase the wait time on each trigger.
5. Keep retry rhythm conservative; do not switch back to parallel mode in the same task/session.
6. If rate limit continues after retries are exhausted, return rate-limit error and stop OpenSubtitles branch.
7. Operational details and wording source of truth are maintained in `references/opensubtitles.md`.

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
7. OpenSubtitles fallback query strategy is mandatory for every OpenSubtitles search call and must not be skipped.
8. OpenSubtitles download by ranked candidate must call the same mandatory fallback-aware search path before selecting candidate rank.
9. OpenSubtitles credentials must be passed as explicit parameters; environment-variable-only credential path is not allowed.
10. After any OpenSubtitles rate-limit signal, OpenSubtitles calls must run in serial delayed mode; parallel burst is forbidden.
11. Every OpenSubtitles search request must include both primary and normalized query parameters.
12. Fallback order must be executed inside MCP/CLI C# implementation, not by skill-side parallel fan-out.
13. Skill must orchestrate OpenSubtitles `search` -> `download` in strict serial order; no parallel calls.

## Operational Notes

1. Prefer deterministic behavior over creative rewriting.
2. Keep translation natural and context aware, but preserve subtitle pacing.
3. For command details and troubleshooting, read `references/commands.md` and `references/troubleshooting.md`.
4. For literary or entertainment subtitles (jokes, sarcasm, taboo language, sexual humor, dark comedy), strongly prefer an uncensored model variant. Censored models are more likely to weaken punchlines, skip sensitive phrasing, or leave source fragments untranslated.
5. In agent scenarios, prefer MCP mode with explicit plan steps. Reusing MCP sampling through the existing client session can reduce token spend and lower deployment/ops overhead compared with standing up separate external-only translation services.



