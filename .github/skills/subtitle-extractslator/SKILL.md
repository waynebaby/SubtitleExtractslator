---
name: subtitle-extractslator
description: Probe media subtitle tracks, search OpenSubtitles candidates, extract fallback subtitles, and run grouped context-aware translation while preserving SRT timing and structure. Use when user asks to find subtitle language, reuse existing subtitle tracks, check OpenSubtitles, or produce translated subtitle files with stable rhythm and timeline.
compatibility: Designed for agent environments (GitHub Copilot, Claude Code, OpenClaw, Codex) with Python 3 bootstrap access, native executable launch permissions, network access for NuGet version checks, and FFmpeg available.
license: MIT
metadata:
  author: waynebaby
  version: 0.1.19
  channel: beta
  mcp-server: subtitle-extractslator
  category: subtitle-translation
  language: zh-CN
---

# Subtitle Extractslator Skill

## Purpose

This repository is skill-first: the subtitle skill package is the primary deliverable, and execution authority is governed by Loom SO.

**This skill has been enhanced by Loom SO and is now SO-exclusive governed (Released channel, locked to 0.3.328).** Only `so.exe run` and `so.exe resume` on Windows (`so run` and `so resume` on Unix) count as official skill runs and official skill execution history. Direct CLI and direct MCP are runtime primitives for component operations only.

Deterministic orchestration is encoded in the checked-in workflow JSON template and validated by SO runtime 0.3.328 (see `Workflow Contract` below). The checked-in template compiles on that runtime; end-to-end runs still need real external results and inputs. The authoritative runtime lock is `assets/so-workflow/so-package-lock.json`. Runtime contracts remain in `references/` for SO-orchestrated implementation.

Normal governance and maintenance for this skill must stay on the direct apphost path (`--guide`, `compile`, `run`, `resume`). Do not treat direct edits to `assets/so-workflow/so-template.json` as a routine operating path. Touch that JSON only as a minimal last-resort workaround when execution is completely blocked and the user explicitly authorizes it, then return immediately to `so.exe compile` and the governed SO path.

## Installation and Release Links

Use this repository's package index pages as the canonical runtime source. This skill package is binary-free and intentionally does not ship `dll` or `bin` runtime assets.

- Project URL: [waynebaby/SubtitleExtractslator](https://github.com/waynebaby/SubtitleExtractslator)
- Stable package index: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- Stable package index (zh-CN): [packages.released.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md)
- Beta package index: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Beta package index (zh-CN): [packages.beta.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md)
- SO package index (Current source of truth): [Techne Loom packages.beta.md](https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md)
- SO guide (Current source of truth, zh-CN): [Techne Loom so-guide.md](https://github.com/waynebaby/Techne-Loom/blob/development/docs/zh-cn/reference/products/so-guide.md)
- SO package index (Released reference): [Techne Loom packages.released.md](https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md)
- SO guide (Released reference): [Techne Loom so-guide.md](https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/so-guide.md)
- Runtime fallback `.nupkg` links are maintained inside the package index pages above.
- Runtime missing diagnosis and fallback guide: `references/binary-missing.md`

SO workflow files in this skill package:
1. Per-run plan — runtime-owned under the execution output root; not shipped with this skill
2. `assets/so-workflow/so-template.json` — Workflow JSON template (execution authority)
3. `assets/so-workflow/so-package-lock.json` — Authoritative SO runtime version lock
4. `assets/bootstrap/restore_runtime.py` — Host RID detection and channel-specific NativeAOT NuGet restore/cache bootstrap
5. External audit artifacts — Compile validation and run/resume audit evidence must stay outside the skill folder

Workflow modification confirmation loop for maintainers:
1. Write the per-run plan under the execution output root first and keep governance changes plan-first; do not add a plan file to this skill.
2. Validate the current `assets/so-workflow/so-template.json` with the locked `0.3.328` SO apphost (`so.exe` on Windows, `so` on Unix) and an external audit root. Compile a copy outside the skill folder, never the checked-in file.
3. Review Mermaid, HTML, workflow backup, and `workflow.analysis.json`.
4. If governance, seam ownership, or route coverage is still unsatisfied, revise the plan and recompile again.
5. Only when execution is completely blocked and the user explicitly permits a minimal workaround may you make the smallest necessary edit to `assets/so-workflow/so-template.json`; then immediately recompile and continue on the direct apphost path.
6. Update this `SKILL.md` only after the compiled workflow is accepted.

Official SO guide refresh for governed maintenance and validation:

```bash
so.exe --guide
```

Resolve the channel's latest native runtime before CLI or MCP operations. The bootstrap prints the absolute executable path; use that path as `<cli_entry>`:

```bash
python3 assets/bootstrap/restore_runtime.py --channel beta
```

On Windows, use `py -3` if `python3` is not on PATH.

Official skill execution entry:

```bash
so.exe run --workflow-file <runtime-workflow-copy>.json
so.exe resume --workflow-file <runtime-workflow-copy>.json --result-file <external-result>.json
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

The per-run plan is maintainer-side context outside this skill. Public `so.exe compile` validates the existing workflow JSON template; it does not accept a plan file as a CLI input.

Routine governed work is plan-first and apphost-validated. Direct edits to `assets/so-workflow/so-template.json` are exception-only and require a completely blocked path plus explicit user permission for a minimal workaround.

**Compilation Authority**: Validate with:
```bash
so.exe compile \
  --workflow-file assets/so-workflow/so-template.json \
  [--audit-output <external-audit-root>]
```

**Execution**: Run via SO runtime against a runtime copy outside the skill folder:
```bash
so.exe run --workflow-file <runtime-workflow-copy>.json [--audit-output <external-audit-root>]
so.exe resume --workflow-file <current>.json --result-file <external-result>.json
```

Before `so.exe resume` on any auth-related or other external seam, validate that the current waiting node, the resume result ID, and the active context/output-policy snapshot all point to the same seam completion. Do not reuse stale seam artifacts.

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

**Named subagent fallback** (`SubagentCall` worker seam):
1. Invoke the worker subagent by its exact name first.
2. If the host reports that the exact name is not registered, do not substitute a similar role. Invoke an available registered generic subagent as the driver, passing the exact file path `references/worker.md`, its full contents, every batch input, and the reference context. The driver must perform the worker contract and return its handoff.
3. A read-only or exploration-only role is not valid for the worker, because the worker writes subtitle outputs and centralized queue state.
4. If no capable driver can run, stop with a concrete blocker and keep the failed evidence. A main-agent fallback requires explicit user approval.
5. Only a platform with no subagent support at all runs the bounded worker inline, as described in [worker playbook](references/worker.md).

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
12. If probe or extract finds no usable embedded subtitle track, do not stop the batch. Continue deterministic fallback in order: local subtitle discovery, then OpenSubtitles search/download, then translation if needed.
13. For embedded Chinese subtitle discovery, treat `zh` and `chi` as equivalent fallback preferences before declaring Chinese subtitles unavailable.
14. Keep normal default output paths for non-subtitle artifacts. For media-file requests, additionally copy the final subtitle beside the source video and name that copied subtitle `<original_video_basename>.<lang>.srt`. Only change that copied subtitle destination or name when the user explicitly requests it.
15. Keep MCP orchestration agent-driven and avoid script-driven tool loops.
16. Keep `subtitle-extractslator/` binary-free; acquire runtime from this repository's `packages.*.md` absolute URLs.
17. Never use workflow nodes or steps equivalent to `run a multistep plan`; this pattern is prohibited because it weakens SO governance boundaries and can expose execution-leak paths.
18. Do not directly edit `assets/so-workflow/so-template.json` as normal maintenance. Only when the governed path is completely blocked and the user explicitly allows it may a minimal workaround be applied, followed immediately by `so.exe compile` and continued SO-governed execution.
19. When a named worker subagent is not registered, use the generic driver fallback in `references/worker.md`. Never substitute a similar role or run the batch inline without explicit user approval.

## Operational Notes

1. Prefer deterministic behavior over creative rewriting.
2. Keep translation natural and context-aware while preserving subtitle pacing.
3. For commands and troubleshooting, use `references/cli.md` and `references/troubleshooting.md`.
4. For long-running folder jobs, use `references/batching.md`, `references/supervisor.md`, and `references/worker.md`.
5. Platform-specific agent files are optional adapters; runtime behavior is defined by this skill and `references/` contracts.
6. Local translation or bitmap-OCR HTTP endpoints must pass a real health check before use; startup logs such as `READY` are not sufficient evidence that the endpoint is actually listening and usable.

















