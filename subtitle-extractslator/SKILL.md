---
name: subtitle-extractslator
description: Probe media subtitle tracks, search OpenSubtitles candidates, extract fallback subtitles, and run grouped context-aware translation while preserving SRT timing and structure. Use when user asks to find subtitle language, reuse existing subtitle tracks, check OpenSubtitles, or produce translated subtitle files with stable rhythm and timeline.
compatibility: Designed for agent environments (GitHub Copilot, Claude Code, OpenClaw, Codex) with local executable access and FFmpeg available.
license: MIT
metadata:
  author: waynebaby
  version: 0.1.8
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
- CLI command and auth-contract examples
- output path policy
2. `references/mcp.md`:
- MCP-first policy and setup contract
- exposed tools and return contract
- MCP runtime notes and constraints
3. `references/opensubtitles.md`:
- OpenSubtitles auth-command credential contract (CLI + MCP)
- search/download fallback strategy, rate-limit handling, and parameter matrix
4. `references/troubleshooting.md`:
- failure patterns and diagnostics checklist

## Workflow Contract

Follow this exact decision tree (do not reorder):

MCP invocation discipline (CRITICAL):
NO SCRIPTS IN MCP.
1. In MCP mode, tools must be invoked by the AI agent manually one-by-one in strict sequence (not human hand-operated steps).
2. For all MCP steps, do not use scripts or batched wrappers to drive tool calls.
3. Subagent fanout is allowed when it runs through the agent path and can use MCP sampling context.
4. Subagent fanout does not override tool-level serial constraints (for example OpenSubtitles `search -> download` remains strict serial).
5. Reason: script-driven/non-agent calls can bypass agent sampling context and break MCP sampling behavior.

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
OpenSubtitles rules (CRITICAL):
1. Every `opensubtitles_search` call must include both `searchQueryPrimary` and `searchQueryNormalized`.
2. Skill-side orchestration must be strictly linear: `opensubtitles_search` -> `opensubtitles_download` only. No parallel fan-out.
3. Fallback sequence is internal C# only and mandatory:
- primary + target language
- normalized + target language
- primary + any language
- normalized + any language
4. Before OpenSubtitles operations, ensure auth is available via `subtitle auth login` / `subtitle auth aquire` flow.
5. `opensubtitles_download` is download-only and must use `fileId` from search candidate.
6. Candidate `fileId` may be null; null `fileId` candidates are not valid for download-by-id.
7. Target-language strict review rule:
- when target-language search returns many candidates and filename confidence is low, run strict review before final adoption
- review step A: compare source/candidate names after removing season/episode tokens (`SxxEyy`, `xxyy`); non-episode tokens must remain similar
- review step B: run timing-check interface against downloaded candidate subtitle:
  - MCP: `subtitle_timing_check`
  - CLI: `subtitle-timing-check`
  - acceptance rule: `abs(video_duration - subtitle_last_cue_end) < 600 seconds`
- if any step fails, skip current candidate and continue next candidate
8. Post-download branch:
- if downloaded subtitle already matches target language: finish (no translation)
- otherwise: continue `translate`
9. Rate limit (HTTP `429` or equivalent) handling:
- switch to serial + delayed mode immediately
- retry each request up to 20 times with increasing delay
- stop OpenSubtitles branch when retries are exhausted
10. Detailed input/output matrix is maintained in `references/opensubtitles.md`.

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
When sampling fails (including missing MCP server instance injection during `translate`), return error directly.
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
9. OpenSubtitles auth must follow auth command flow (`login/aquire/status/clear`); do not use per-call username/password input.
10. After any OpenSubtitles rate-limit signal, OpenSubtitles calls must run in serial delayed mode; parallel burst is forbidden.
11. Every OpenSubtitles search request must include both primary and normalized query parameters.
12. Fallback order must be executed inside MCP/CLI C# implementation, not by skill-side parallel fan-out.
13. Skill must orchestrate OpenSubtitles `search` -> `download` in strict serial order; no parallel calls.

## Operational Notes

1. Prefer deterministic behavior over creative rewriting.
2. Keep translation natural and context aware, but preserve subtitle pacing.
3. For command details and troubleshooting, read `references/cli.md` and `references/troubleshooting.md`.
4. For literary or entertainment subtitles (jokes, sarcasm, taboo language, sexual humor, dark comedy), strongly prefer an uncensored model variant. Censored models are more likely to weaken punchlines, skip sensitive phrasing, or leave source fragments untranslated.
5. In agent scenarios, prefer MCP mode with explicit plan steps. Reusing MCP sampling through the existing client session can reduce token spend and lower deployment/ops overhead compared with standing up separate external-only translation services.









