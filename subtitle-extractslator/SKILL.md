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

This skill orchestrates subtitle discovery and translation using the local CLI and optional MCP mode.

Primary goals:
1. Keep timeline and subtitle structure stable.
2. Prioritize existing subtitle resources before extraction.
3. Use grouped rolling context for better semantic consistency.

## Trigger Guidance

Use this skill when user asks to:
1. Check whether a media file already has a specific subtitle language.
2. Search online subtitle candidates before local extraction.
3. Translate subtitles while preserving SRT timing and segmentation rhythm.
4. Produce a final SRT file from a media file or existing subtitle file.

## Runtime Paths

Windows binary path inside this skill:
`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe`
*use  [binary] --help* to get detailed CLI usage.

CLI mode examples:
1. Probe:
`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli probe --input "movie.mkv" --lang zh`
2. Search OpenSubtitles candidates:
`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli opensubtitles-search --input "movie.mkv" --lang zh`
3. Full workflow:
`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt"`

MCP tool names:
1. probe
2. opensubtitles_search
3. extract
4. run_workflow

## Workflow Contract

Follow this exact order:

0. Confirm user target: input file and target language.
1. Probe internal subtitle tracks.
If target language exists, report and stop.
2. Query OpenSubtitles candidates.
If user mention not using open subtitles or no candidates, continue next.
If candidates exist,  adopt the most popular candidate. download, rename to output path.
3. Extract local subtitle.
Prefer English track.
If English not present, pick deterministic nearest-language fallback.
4. Build timeline cue objects and split into groups.
Rule A: if current stack is non-empty and no dialogue for one minute, start new group.
Rule B: if group exceeds 100 cues, start new group.
5. Rolling context update.
For each group, generate scene summary and update historical core knowledge state.
6. Translate each group using scene summary plus historical knowledge plus group timeline objects.
Preserve index, timestamps, line counts, and segmentation rhythm.
7. Merge all translated groups into final SRT.

## Mode-Aware Translation Source Policy

1. MCP mode:
Sampling first, external provider fallback.
2. Non-MCP mode:
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
