# Subtitle Extractslator SO Enhancement Documentation

## Overview

This document explains how the subtitle-extractslator skill has been enhanced with SkillOrchestrator (SO) deterministic orchestration.

## Enhancement Status

- **Status**: SO-Exclusive Governed Implementation
- **Version**: 0.1.14
- **SO Channel**: Beta (development branch)
- **SO Runtime Version**: 0.2.91-beta
- **SO Package Index**: [Techne Loom packages.beta.md](https://github.com/waynebaby/Techne-Loom/blob/development/packages.beta.md)
- **SO Guide**: [Techne Loom so-guide.md](https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/so-guide.md)

## What Changed

### Before Enhancement

- Skill was prose-defined with reference contracts in `references/` folder
- Runtime behavior was implicitly coordinated by agents using tool sequences
- No explicit execution model or deterministic state tracking
- Harder to diagnose execution flow or replay failures

### After Enhancement

- Explicit **deterministic workflow JSON** (`assets/so-workflow/so-template.json`) defines all states, transitions, and decision points
- **Skill plan** (`assets/so-workflow/skill-plan.md`) documents the orchestration intent
- SO runtime **validates** the existing workflow JSON via `compile` before execution
- SO runtime **executes** deterministically with explicit weave-out points for external actions
- **Audit artifacts** (Mermaid visualizations, event logs) provide execution transparency

## Key Files

### 1. Skill Plan (`assets/so-workflow/skill-plan.md`)

- **Purpose**: High-level maintainer-facing orchestration plan used to review and evolve the workflow template
- **Content**:
  - Package channel and SO authority references
  - Goal and deterministic node design
  - External seam classifications (AskUser, McpCall, SubagentCall, WaitResume)
  - Guardrails and success criteria
- **Maintenance**: Update this when adding new deterministic flows or changing external seam behavior; `compile` does not read this Markdown file as a CLI input

### 2. Workflow Template (`assets/so-workflow/so-template.json`)

- **Purpose**: Deterministic workflow JSON execution model validated by SO `compile` (execution authority)
- **Structure**:
  - **Nodes**: 80+ state nodes representing decision points, subagent seams, ask prompts, memory operations
  - **Transitions**: 150+ explicit transitions with branch conditions
  - **Start/Current**: Initial node and execution position
  - **Status**: `template` (not yet run) → `active` (running) → `completed` (done)
  - **Context**: Metadata including skill version, SO version, package indices
- **Key Node Types**:
  - `state` (State): Owning workflow node that hosts the outgoing transition group for each decision point
  - `condition`: Branch on deterministic evaluation
  - `subagent` (SubagentCall): Delegate CLI-side runtime work and deterministic helper steps to an outer skill-runner agent, then resume with structured payload
  - `tool` (ToolCall): Reserved for true SO built-in tool surfaces; the governed template should not depend on unresolved custom tool names here
  - `ask` (AskUser): Request user input for unresolved decisions
  - `wait` (WaitResume): Pause for external signal
  - Current validated template mix: `modelThink`, `mcpCall`, `subagentCall`, `askUser`, `conditionBranch`, `waitResume`

### 3. Audit Output (`assets/so-workflow/audit/`)

- **Mermaid Markdown** (`*.mermaid.md`): Visual flowchart of workflow
- **HTML** (`*.html`): Interactive HTML visualization
- **JSON Backup** (`workflow.backup.json`): Workflow state at each compilation
- **Event Log** (`*.events.jsonl`): Append-only execution event stream

## Deterministic Flow Breakdown

### Phase 1: Normalization & Mode Detection

```text
start → normalize_request → validate_input → check_mode
                                               ├→ [MCP] mcp_preflight
                                               └→ [CLI] read_local_paths
```

- Parse input parameters
- Determine media vs SRT
- Route MCP or CLI based on configuration

### Phase 2: Preflight Setup

```text
[MCP] → mcp_setup_check ─┬→ [NeedsSetup] ask_mcp_setup → apply_mcp_setup
                         └→ [Ready] read_local_paths
                           
read_local_paths → check_ffmpeg ─┬→ [Missing] ask_ffmpeg
                                 └→ [Found] route_request_kind
```

- Verify MCP readiness
- Load FFmpeg path from persistent storage
- Request user setup if needed

### Phase 3: Request Routing

```text
route_request_kind ─┬→ [Single] single_check_input_kind
                    └→ [Batch] batch_init
```

Determines single-run vs batch processing path.

### Phase 4: Source Selection (Single Run)

```text
single_check_input_kind ─┬→ [Media] probe_media → target_track_check
                         └→ [SRT] check_local_subtitle

target_track_check ─┬→ [Exists] emit_existing_target → done
                    └→ [Missing] check_local_subtitle

check_local_subtitle → route_source_selection ─┬→ [Found] group_translation_input
                                               └→ [NotFound] opensubtitles_search
                                               
opensubtitles_search → opensubtitles_candidate_check ─┬→ [Found] ask_candidate_adoption
                                                      └→ [NotFound] local_extract_route
```

Deterministic priority: embedded → local file → OpenSubtitles → extract fallback

### Phase 5: Translation Pipeline

```text
group_translation_input → update_translation_memory 
                       → route_translation_engine ─┬→ [MCP] mcp_translate
                                                    └→ [Model] model_translate
                       → merge_translation 
                       → mux_check ─┬→ [Yes] mux_output
                                    └→ [No] emit_single_summary → done
```

- Parse SRT into groups (preserving timing)
- Translate each group with rolling context memory
- Merge translated groups back into SRT
- Optional muxing into media file

### Phase 6: Batch Processing

```text
batch_init → batch_build_queue → batch_dispatch_check ─┬→ [Pending] batch_route_worker
                                                        └→ [Empty] emit_batch_summary → done

batch_route_worker ─┬→ [Inline] batch_inline_worker
                    └→ [Subagent] batch_subagent_worker
                    
[Worker] → batch_apply_deltas → batch_cooldown_check ─┬→ [CooldownNeeded] batch_wait_resume
                                                       └→ [Continue] batch_dispatch_check
```

- Queue management
- Per-item worker execution (inline or delegated)
- Rate-limit handling with cooldown

## External Seams (Weave-Out Points)

When SO encounters a weave-out, it returns a `<so_property>` JSON payload with:

- `status: blocked`
- `current_step_kind`: Type of blocking step (AskUser, McpCall, WaitResume, SubagentCall)
- `skill_hint`: Literal instruction for next action
- `required_inputs`: Schema for structured response (if needed)

### AskUser Points

1. **MCP Setup**: `ask_mcp_setup` — Permission to configure MCP for this workspace
2. **FFmpeg Path**: `ask_ffmpeg` — Directory containing ffmpeg and ffprobe
3. **Candidate Selection**: `ask_candidate_adoption` — Confirm OpenSubtitles candidate

### McpCall Points

- `probe`: Check embedded subtitle tracks
- `extract`: Extract subtitle from media
- `opensubtitles_search`: Search OpenSubtitles
- `opensubtitles_download` / `cli_download_candidate`: Download candidate
- `subtitle_timing_check`: Validate timing accuracy
- `mcp_translate`: Translate subtitle groups

### WaitResume Points

- `batch_wait_resume`: Pause for rate-limit cooldown or external signal

### SubagentCall Points

- `normalize_request`: Populate the initial routing payload instead of relying on implicit in-engine mutation
- `ensure_cli_runtime`: Verify portable CLI host readiness and bootstrap minimal .NET runtime when needed
- `read_localpaths`: Load remembered local runtime paths before probing or extraction
- `batch_subagent_worker`: Delegate bounded batch work to worker subagent
- Other CLI-side runtime helpers such as FFmpeg readiness, OpenSubtitles auth/search/download, local extraction, bitmap OCR reconstruction, translation grouping, rolling-memory updates, summary emission, merge, mux, and queue reconciliation

## Compilation & Validation

To validate the workflow template:

```bash
cd e:\code\g\SubtitleExtractslator

# Set SO runtime path
$soPath = Join-Path $env:TEMP 'techne-loom-so-runtime-v0291-beta/expanded/Techne.Loom.SkillOrchestrator.latest/lib/net9.0/so.dll'

# Compile with validation
dotnet $soPath compile `
  --workflow-file .github/skills/subtitle-extractslator/assets/so-workflow/so-template.json `
  --audit-output artifacts/so-compile-audit

# Check output
Get-Item artifacts/so-compile-audit/*.mermaid.md
Get-Item artifacts/so-compile-audit/*.html
```

**Compilation outputs**:

- Mermaid Markdown visualization
- Interactive HTML diagram
- Workflow JSON backup for comparison
- Workflow analysis report for gate/seam review

## Execution & Resume

### Single Run

```bash
cp .github/skills/subtitle-extractslator/assets/so-workflow/so-template.json artifacts/so-run-audit/workflow.current.json
dotnet $soPath run `
  --workflow-file artifacts/so-run-audit/workflow.current.json `
  --audit-output artifacts/so-run-audit
```

### Resume from Weave-Out

When SO blocks, create a resume result JSON:

```json
{
  "transition_id": "transition.ask-mcp-setup",
  "correlation_key": null,
  "payload": {
    "user_choice": "yes"
  }
}
```

Then resume:

```bash
dotnet $soPath resume `
  --workflow-file <current-workflow>.json `
  --result-file <resume-result>.json
```

## Integration with Existing Runtime

### CLI Reference (`references/cli.md`)

- Unchanged: All CLI commands documented in `references/cli.md` remain the same
- Integration: SO delegates CLI-side runtime work through `SubagentCall` seams; the outer skill-runner agent executes the documented CLI command or equivalent deterministic local step, then resumes the workflow with the declared outputs
- Example: `cli_download_candidate` seam runs `SubtitleExtractslator.Cli.dll opensubtitles-download`, captures `downloaded_subtitle_path` / `timing_check_required`, and resumes the workflow

### MCP Reference (`references/mcp.md`)

- Unchanged: MCP tools and policy remain documented
- Integration: SO calls MCP tools via `McpCall` weave-out when MCP mode is active
- Example: `mcp_translate` node weaves out to MCP translate tool

### Other References

- `references/opensubtitles.md`: Deterministic search/download strategy preserved
- `references/localpaths.md`: FFmpeg path persistence unchanged
- `references/batching.md`: Batch queue contract unchanged
- `references/supervisor.md`: Supervisor/worker coordination unchanged
- `references/worker.md`: Per-item worker contract unchanged

## Guardrails Preserved

All existing guardrails are now explicit SO transition conditions:

1. ✅ Preserve timestamps and cue ordering
2. ✅ Do not merge/split cues (unless explicitly requested)
3. ✅ Stop on structural validation failure
4. ✅ Preserve deterministic source selection order
5. ✅ Attempt local discovery before OpenSubtitles
6. ✅ Keep OpenSubtitles search → download serial
7. ✅ Require both `searchQueryPrimary` and `searchQueryNormalized`
8. ✅ Keep OpenSubtitles fallback order inside runtime (not skill-side fanout)
9. ✅ Use OpenSubtitles auth cache flow
10. ✅ Switch to rate-limit cooldown mode after 429
11. ✅ Keep queue state in centralized temp storage
12. ✅ Keep MCP orchestration agent-driven
13. ✅ Keep skill binary-free

## Troubleshooting

### Compile Fails

**Error**: "Unable to locate repository root"

**Solution**: Ensure SO runtime is run from git repository root:

```bash
cd e:\code\g\SubtitleExtractslator  # Must be repo root
```

### Workflow Won't Start

**Check**:

1. `skill-plan.md` exists and is valid Markdown
2. `so-template.json` passes compilation
3. Initial context contains required parameters (inputFile, targetLanguage, etc.)

### Execution Blocks Unexpectedly

**Check**: Examine `current_step_kind` in SO return payload. If `AskUser` or `McpCall`, provide required response via `resume`.

### Audit Artifacts Missing

**Check**: Ensure `--audit-output` path is writable:

```bash
New-Item -ItemType Directory artifacts/so-audit -Force
```

## Future Enhancements

1. **Context File Support**: Add `--context-file` support to pre-populate parameters
2. **Parallel Translation Groups**: Investigate parallelization of non-serial translation groups
3. **Advanced Scheduling**: Enhanced cooldown and retry strategies for rate-limiting
4. **Cross-Platform Testing**: Validate on macOS and Linux with different SO runtimes
5. **Memory Optimization**: Reduce workflow JSON size for very large batch queues

## References

- [Techne Loom SO Guide](https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/so-guide.md)
- [Workflow Terminology](https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/architecture/workflow-terminology.md)
- [Skill Enhancement Guide](https://github.com/waynebaby/SubtitleExtractslator/blob/main/docs/skill-installation-and-build.md)

---

**Last Updated**: 2026-06-11
**Enhancement Version**: 1.0
**SO Channel**: Beta (v0.2.1+)
