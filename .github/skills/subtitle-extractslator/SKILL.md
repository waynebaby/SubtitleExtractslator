---
name: subtitle-extractslator
description: Probe media subtitle tracks, search OpenSubtitles candidates, extract fallback subtitles, and run grouped context-aware translation while preserving SRT timing and structure. Use when user asks to find subtitle language, reuse existing subtitle tracks, check OpenSubtitles, or produce translated subtitle files with stable rhythm and timeline.
compatibility: Designed for agent environments (GitHub Copilot, Claude Code, OpenClaw, Codex) with local executable access and FFmpeg available.
license: MIT
metadata:
  author: waynebaby
  version: 0.1.18
  mcp-server: subtitle-extractslator
  category: subtitle-translation
  language: zh-CN
---

# Subtitle Extractslator Skill

## Purpose

This repository is skill-first: the subtitle skill package is the primary deliverable, and execution authority is governed by Loom SO.

**This skill has been enhanced by Loom SO and is now SO-exclusive governed (Beta channel, locked to 0.2.91-beta).** Only `dotnet so.dll run` and `dotnet so.dll resume` count as official skill runs and official skill execution history. Direct CLI and direct MCP are runtime primitives for component operations only.

Deterministic orchestration is encoded in the checked-in workflow JSON template and validated by SO runtime 0.2.91-beta (see `Workflow Contract` below). The authoritative runtime lock is `assets/so-workflow/so-package-lock.json`. Runtime contracts remain in `references/` for SO-orchestrated implementation.

## Installation and Release Links

Use this repository's package index pages as the canonical runtime source. This skill package is binary-free and intentionally does not ship `dll` or `bin` runtime assets.

- Project URL: [waynebaby/SubtitleExtractslator](https://github.com/waynebaby/SubtitleExtractslator)
- Stable package index: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- Stable package index (zh-CN): [packages.released.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md)
- Beta package index: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Beta package index (zh-CN): [packages.beta.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md)
- SO package index (Current Beta source of truth): [Techne Loom packages.beta.md](https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md)
- SO guide (Current Beta source of truth, zh-CN): [Techne Loom so-guide.md](https://github.com/waynebaby/Techne-Loom/blob/development/docs/zh-cn/reference/products/so-guide.md)
- SO package index (Released reference): [Techne Loom packages.released.md](https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md)
- SO guide (Released reference): [Techne Loom so-guide.md](https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/so-guide.md)
- Runtime fallback `.nupkg` links are maintained inside the package index pages above.
- Runtime missing diagnosis and fallback guide: `references/binary-missing.md`

SO workflow files in this skill package:
1. `assets/so-workflow/skill-plan.md` — Supporting maintainer-facing orchestration plan
2. `assets/so-workflow/so-template.json` — Workflow JSON template (execution authority)
3. `assets/so-workflow/so-package-lock.json` — Authoritative SO runtime version lock
4. External audit artifacts — Compile validation and run/resume audit evidence must stay outside the skill folder

Workflow modification confirmation loop for maintainers:
1. Update `assets/so-workflow/skill-plan.md` or `assets/so-workflow/so-template.json`.
2. Validate `assets/so-workflow/so-template.json` with the locked `0.2.91-beta` SO runtime and an external audit root.
3. Review Mermaid, HTML, workflow backup, and `workflow.analysis.json`.
4. If governance, seam ownership, or route coverage is still unsatisfied, revise and recompile again.
5. Update this `SKILL.md` only after the compiled workflow is accepted.

Official SO guide refresh for governed maintenance and validation:

```bash
dotnet so.dll --guide --lang zh-cn
```

Component primitive guide entry (direct CLI runtime diagnostics only):

```bash
dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide
```

Official skill execution entry:

```bash
dotnet so.dll run --workflow-file <runtime-workflow-copy>.json
dotnet so.dll resume --workflow-file <runtime-workflow-copy>.json --result-file <external-result>.json
```

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
- runtime package acquisition and CLI primitive command surface
- CLI command and auth-contract examples
- output path policy
2. `references/mcp.md`:
- MCP primitive policy and setup contract
- exposed tools and return contract
- MCP runtime notes and constraints
3. `references/opensubtitles.md`:
- OpenSubtitles auth-command credential contract (CLI + MCP)
- search/download fallback strategy, rate-limit handling, and parameter matrix
4. `references/troubleshooting.md`:
- failure patterns and diagnostics checklist
5. `references/binary-missing.md`:
- release download links for current version binaries
- binary missing diagnosis, validation checklist, and recovery flow
6. `references/localpaths.md`:
- local machine path memory (for example FFmpeg bin path)
- persisted records for next skill run
7. `references/batching.md`:
- long-run queue batching and resume policy
- centralized temp tracking file contract for multi-file jobs
8. `references/supervisor.md`:
- persistent coordinator playbook for multi-file runs
- queue ownership, batch selection, and resume behavior
9. `references/worker.md`:
- bounded batch execution playbook
- per-item completion/failure handoff contract

## Workflow Contract

SO template (`assets/so-workflow/so-template.json`) is the canonical and exclusive deterministic execution model. Official skill runs and official skill history are SO-owned.

`skill-plan.md` is maintainer-facing planning context. Public `dotnet so.dll compile` validates the existing workflow JSON template; it does not accept `skill-plan.md` as a CLI input.

**Compilation Authority**: Validate with:
```bash
dotnet so.dll compile \
  --workflow-file assets/so-workflow/so-template.json \
  [--audit-output <external-audit-root>]
```

**Execution**: Run via SO runtime against a runtime copy outside the skill folder:
```bash
dotnet so.dll run --workflow-file <runtime-workflow-copy>.json [--audit-output <external-audit-root>]
dotnet so.dll resume --workflow-file <current>.json --result-file <external-result>.json
```

**High-level flow**:
1. Normalize input (media/SRT, target language, output path).
2. Route execution mode (MCP vs CLI).
3. Probe embedded tracks → check local files → OpenSubtitles search/download.
4. Translate via grouped context-aware processing.
5. Merge and emit final SRT.
6. Update batch queue state (if applicable).

**External seams (weave out)**:
- `AskUser`: MCP setup, FFmpeg path, candidate selection, explicit policies
- `McpCall`: probe, extract, search, download, translate tools
- `WaitResume`: batch cooldown, external async triggers
- `SubagentCall`: worker batch delegation

## Guardrails

1. Preserve timestamps and cue ordering.
2. Do not merge/split cues unless user explicitly requests it.
3. Stop on structural validation failure; never emit broken SRT.
4. Preserve deterministic source selection order.
5. Attempt local subtitle discovery before OpenSubtitles when embedded tracks are absent.
6. Keep OpenSubtitles `search -> download` strict serial.
7. Require both `searchQueryPrimary` and `searchQueryNormalized` for OpenSubtitles search.
8. Keep OpenSubtitles fallback order inside C# runtime, not skill-side parallel fanout.
9. Keep OpenSubtitles auth in `login/aquire/status/clear` cache flow.
10. Switch OpenSubtitles lane to delayed serial mode after any rate-limit signal.
11. Keep queue state in centralized temp storage, never beside media files.
12. Keep MCP orchestration agent-driven and avoid script-driven tool loops.
13. Keep `subtitle-extractslator/` binary-free; acquire runtime from this repository's `packages.*.md` absolute URLs.
14. Never use workflow nodes or steps equivalent to `run a multistep plan`; this pattern is prohibited because it weakens SO governance boundaries and can expose execution-leak paths.

## Operational Notes

1. Prefer deterministic behavior over creative rewriting.
2. Keep translation natural and context-aware while preserving subtitle pacing.
3. For commands and troubleshooting, use `references/cli.md` and `references/troubleshooting.md`.
4. For long-running folder jobs, use `references/batching.md`, `references/supervisor.md`, and `references/worker.md`.
5. Platform-specific agent files are optional adapters; runtime behavior is defined by this skill and `references/` contracts.
















