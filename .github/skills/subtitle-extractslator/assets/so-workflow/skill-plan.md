# Subtitle Extractslator SO Enhancement Plan

**This file is the deterministic orchestration input for SO-based skill enhancement.**

## Package Channel & Authority

- **Channel**: Stable (main)
- **SO Version**: 0.1.22
- **SO Package Index**: [Techne Loom packages.released.md](https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md)
- **SO Guide**: [Techne Loom so-guide.md](https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/so-guide.md)
- **Skill Package Index (Released)**: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- **Skill Package Index (Beta)**: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)

## Goal

Use SO deterministic orchestration as the backbone for:
1. Subtitle discovery (embedded tracks, local files, OpenSubtitles)
2. Subtitle extraction and validation
3. Context-aware grouped translation with rolling memory
4. SRT merge and output with preserved timing/structure
5. Long-running batch processing with queue state management

Preserve all existing runtime constraints:
- Strict deterministic source selection order (embedded → local → OpenSubtitles)
- Serial OpenSubtitles search/download (no parallel fanout)
- Rate-limit handling with delayed retry
- MCP-first policy with CLI fallback
- Preserve timing and cue ordering at all costs

## High-Level Deterministic Flow

### Phase 1: Normalize Request
1. **StateUpdate**: Parse request (input file/SRT, target language, output path)
2. **StateUpdate**: Validate input file exists and determine content kind (media vs SRT)
3. **StateUpdate**: Generate unique run-id for tracking and temp directory
4. **ToolCall**: Read operating mode environment hints (MCP_ENABLED, CLI_ONLY)

### Phase 2: Resolve Runtime & Preflight
1. **ConditionBranch**: Determine execution mode
   - If MCP configured and enabled: → MCP path
   - Otherwise: → CLI path
2. **ToolCall** (MCP mode only): Verify MCP server is reachable
3. **ToolCall**: Read local path memory from `references/localpaths.md`
4. **ToolCall** (if FFmpeg needed): Validate FFmpeg bin path or ask user for path

### Phase 3: Probe Embedded Subtitle Tracks
1. **ToolCall**: Probe media file for existing subtitle tracks
   - If input is SRT: skip probe (no embedded tracks)
   - If input is media: call `probe` with target language
2. **ConditionBranch**: Check probe result
   - Target language track exists: → Jump to emit existing target
   - No target track: → Continue to source selection

### Phase 4: Source Selection (Deterministic Priority)
1. **ToolCall**: Check for local subtitle file by extension pattern
   - Look for `{input_basename}.{lang}.srt` in same folder
   - Look for `{input_basename}.srt` in same folder
2. **ConditionBranch**: Local file found?
   - Yes: → Load local subtitle as source
   - No: → Continue to OpenSubtitles

### Phase 5: OpenSubtitles Search & Download (Serial)
1. **ToolCall**: Ensure OpenSubtitles auth is valid (`subtitle auth aquire`)
2. **ToolCall**: Search OpenSubtitles with deterministic query strategy
   - Primary query: base filename from media
   - Normalized query: series/episode pattern from full path
3. **ConditionBranch**: Candidates found?
   - No candidates: → Fallback to extract (if media) or error (if SRT-only)
   - Candidates found: → Continue
4. **AskUser** (if ambiguous): Ask user to confirm best candidate or use default rank 1
5. **ToolCall**: Download selected candidate
6. **ToolCall**: Optional timing check if confidence < threshold

### Phase 6: Language Check & Translation
1. **ConditionBranch**: Is downloaded subtitle already in target language?
   - Yes: → Jump to merge & write
   - No: → Continue to translation
2. **ToolCall**: Build grouped timeline objects
   - Parse SRT cues into groups (configurable cues per group, e.g., 3-5 cues)
   - Preserve timing, cue id, and sequence
3. **MemoryWrite**: Initialize/read rolling context memory for translation consistency
4. **ToolCall** (via MCP or CLI): Translate each group
   - MCP path: call `translate` tool once per group
   - CLI path: delegate to subagent or direct call
   - Preserve group timing and cue structure
   - Accumulate context memory across groups
5. **MemoryWrite**: Update translation memory after each successful group

### Phase 7: Merge & Write Final SRT
1. **ToolCall**: Merge translated groups back into SRT format
2. **ToolCall**: Validate final SRT structure
3. **ArtifactEmit**: Write final SRT to output path
4. **ArtifactEmit**: Emit summary (source, translation groups, output path)

### Phase 8: Batch Queue Update (if applicable)
1. **ConditionBranch**: Is this part of a batch run?
   - Single run: → Jump to done
   - Batch run: → Update queue files
2. **ToolCall**: Mark current item as completed in centralized queue
3. **ToolCall**: Update `<temp-root>/agent-runs/{run-id}/completed.txt`
4. **StateUpdate**: Prepare next item or signal batch completion

### Phase 9: Terminal States
1. **ArtifactEmit**: Emit final result object (paths, metrics, events)
2. **StateUpdate** (done node): Mark run as completed

## External Seams (Weave Out Points)

### AskUser Points
1. **MCP Setup**: When MCP not configured, ask user permission to set up
2. **Output Policy**: When output path is ambiguous, ask user for explicit path
3. **Candidate Selection**: When multiple OpenSubtitles candidates exist and none scored 100%
4. **FFmpeg Path**: When FFmpeg not found and not in PATH, ask user for bin directory

### McpCall Points
1. **Probe** (MCP mode): Call MCP `probe` tool
2. **Extract** (MCP mode): Call MCP `extract` tool if needed
3. **Subtitle Timing Check** (MCP mode): Call MCP `subtitle_timing_check` tool
4. **OpenSubtitles Search** (MCP mode): Call MCP `opensubtitles_search` tool
5. **OpenSubtitles Download** (MCP mode): Call MCP `opensubtitles_download` tool
6. **Translate** (MCP mode): Call MCP `translate` tool (once per group)

### ModelThink Points
1. **Not used in current deterministic design**: All decisions are made by explicit branching and guardrails

### SubagentCall Points
1. **Batch Worker** (batch mode): Delegate bounded sets of translate jobs to worker subagent
2. **Worker coordination**: Send queue state and per-item translation parameters

### WaitResume Points
1. **None in current design**: SO runs to completion or weaves out for user/external action

## Guardrails & Constraints

1. **Timestamp Preservation**: Never merge or split cues; preserve `start → end` timing exactly
2. **Cue Ordering**: Maintain original cue sequence and numbering
3. **Validation**: Stop on any SRT parse error; never emit broken output
4. **Deterministic Fallback**: Follow exact priority order (embedded → local → search → download)
5. **Serial OpenSubtitles**: No parallel search/download; maintain strict seq serial order
6. **Rate-Limit Handling**: Switch to delayed retry mode after any 429 response
7. **Auth Flow**: Use `login → aquire → status → clear` state machine
8. **MCP-First**: Attempt MCP execution; fall back to CLI only if MCP unavailable
9. **Queue State**: Store in centralized temp directory, never beside media files
10. **Binary-Free Skill**: Do not ship any `.dll` or `.bin`; acquire from package index

## Success Criteria

- Workflow JSON passes `dotnet so.dll compile` without errors
- Compile emits valid Mermaid Markdown and HTML visualization
- Run execution follows deterministic decision tree
- All weave-out points are classified correctly (AskUser, McpCall, SubagentCall, WaitResume)
- Timing and cue structure preserved in final output
- Batch mode queue state correctly tracked and resumable
- All references/\*.md contracts are honored

## References Used

- `references/cli.md`: CLI command and parameter contract
- `references/mcp.md`: MCP policy and tool surface
- `references/commands.md`: Command reference (aligned to --help)
- `references/opensubtitles.md`: OpenSubtitles strategy and rate-limit handling
- `references/localpaths.md`: FFmpeg path persistence
- `references/batching.md`: Batch queue and resume contract
- `references/supervisor.md`: Batch coordinator playbook
- `references/worker.md`: Per-item worker contract
- `references/troubleshooting.md`: Failure patterns and diagnostics

---

**Execution Authority**

After this plan is reviewed, use:

```bash
dotnet so.dll compile \
  --description-file assets/so-workflow/skill-plan.md \
  --workflow-file assets/so-workflow/so-template.json \
  [--audit-output artifacts/so-compile-audit/]
```

Then execute with:

```bash
dotnet so.dll run \
  --workflow-file assets/so-workflow/so-template.json \
  [--context-file context.json] \
  [--audit-output artifacts/so-run-audit/]
```
