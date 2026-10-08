# Subtitle Extractslator

[English](README.md) | [中文](README.zh-CN.md)

SubtitleExtractslator is a subtitle translation skill project.

The primary deliverable in this repository is the skill package (prompts, policy, runtime assets, and usage contract). The .NET CLI and MCP server are runtime implementations that exist to execute this skill reliably in local scripts and agent environments.

Runtime forms in this repository:

- CLI application for local automation
- MCP stdio server for agent-driven workflows

It is built for end-to-end subtitle processing: detect existing tracks, search candidates, extract source subtitles, translate with context-aware batching, and emit final SRT output while preserving subtitle timing and structure.

## Downloads

Install the skill source from the repository:

```bash
npx skills add waynebaby/SubtitleExtractslator --skill subtitle-extractslator
```

The CLI runtime is published as per-RID NativeAOT NuGet packages. Stable and Beta releases do not publish skill ZIPs. Before running CLI or MCP work, the skill bootstrap checks its channel's latest package and restores the current host RID when needed.

Package indexes:

- Stable index: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- Stable index (zh-CN): [packages.released.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md)
- Beta index: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Beta index (zh-CN): [packages.beta.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md)

Bootstrap example (run from the installed skill directory):

```bash
python3 assets/bootstrap/restore_runtime.py --channel stable
```

The bootstrap prints an absolute native executable path. Run guide mode with that path:

```bash
<absolute-path>/SubtitleExtractslator.Cli --guide
```

The native CLI does not require the .NET runtime. Python 3, NuGet HTTPS access, and FFmpeg for media operations are still required. Package indexes list all RID package IDs and exact-version fallback assets.

## SkillOrchestrator (SO) Deterministic Orchestration

This skill is now enhanced with **SkillOrchestrator deterministic workflow** support (Beta).

### What's New

- **Deterministic Workflow**: Explicit execution model defined in `.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json`
- **Skill Plan**: Orchestration intent documented in `.github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md`
- **SO Compilation**: Workflow is validated and compiled before execution via `dotnet so.dll compile`
- **Audit Artifacts**: Mermaid visualizations, HTML diagrams, and event logs for transparency
- **Transparent Weave-Outs**: Explicit external action points (AskUser, McpCall, SubagentCall, WaitResume)

### Quick Start with SO

1. **Validate workflow**:

   ```bash
   dotnet so.dll compile --description-file .github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md \
     --workflow-file .github/skills/subtitle-extractslator/assets/so-workflow/so-template.json
   ```

2. **Execute deterministically**:

   ```bash
   dotnet so.dll run --workflow-file .github/skills/subtitle-extractslator/assets/so-workflow/so-template.json
   ```

3. **Resume from external action**:

   ```bash
   dotnet so.dll resume --workflow-file <current>.json --result-file <external-result>.json
   ```

### Documentation

- [SO Enhancement Guide](docs/so-enhancement-guide.md) — Comprehensive SO integration documentation
- [Skill Plan](.github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md) — Deterministic flow specification
- [SO Guide (Techne Loom)](https://github.com/waynebaby/Techne-Loom/blob/development/docs/en/reference/products/so-guide.md) — SO framework reference
- [Skill SKILL.md](.github/skills/subtitle-extractslator/SKILL.md) — Skill contract and guardrails

<!-- release-links:start -->
- Stable package index: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- Stable package index (zh-CN): [packages.released.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md)
- Beta package index: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Beta package index (zh-CN): [packages.beta.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md)
- Per-RID runtime fallback packages and SHA-512 sidecars are listed in the package index pages above.
<!-- release-links:end -->

## First: Guide-First Runtime Entry

If your goal is to run this as a skill in your own agent, install the skill from the repository and use the bootstrap-resolved NativeAOT executable as the CLI/MCP command source.

1. Install the source skill from the repository.
2. Run `assets/bootstrap/restore_runtime.py --channel <metadata.channel>` before CLI/MCP or subtitle operations.
3. Run `--guide` with the absolute executable path printed by the bootstrap.
4. If NuGet is unavailable, use the matching RID/version `.nupkg` and `.sha512` assets from the selected channel's fallback release.

Notes:

- This repository keeps `.github/skills/subtitle-extractslator/` for skill routing and policy context.
- The `.github/skills/subtitle-extractslator/` skill package is binary-free and does not ship `assets/bin/`.
- The `SubtitleExtractslator.Cli/` project is the runtime source; per-RID NativeAOT NuGet packages provide CLI + MCP executables.
- For the SO-enhanced skill, `.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json` is the execution basis; `skill-plan.md` is compile input only.
- Build and packaging details are in `docs/skill-installation-and-build.md`.

### Usage Scenarios (Short Prompts)

Use the skill name in your agent chat:

```text
/subtitle-extractslator
```

Scenario 1: Translate one video to Chinese with local model endpoint

```text
/subtitle-extractslator

Translate D:\media\xxx.mkv to zh.
Use local model endpoint http://127.0.0.1:1234/v1/chat/completions. model: qwen3.5-9b-uncensored-hauhaucs-aggressive
Output D:\media\xxx.zh.srt.
```

Scenario 2: Process one folder recursively

```text
/subtitle-extractslator

Run in MCP mode.
Process D:\tv\Fallout\S01 recursively to zh.
Skip files that already have .zh.srt.
```

Scenario 3: Resume interrupted folder run

```text
/subtitle-extractslator

Continue previous D:\tv\Fallout run to zh.
Resume from centralized temp queue state.
```

Scenario 4: Translate one SRT to multiple languages

```text
/subtitle-extractslator

Translate D:\subs\episode01.en.srt to zh, ja, es.
Keep timing and cue order unchanged.
```

Scenario 5: Probe-only check

```text
/subtitle-extractslator

Probe D:\media\xxx.mkv for embedded zh subtitle track.
Return probe result only.
```

Scenario 6: Batch processing with supervisor/worker

```text
/subtitle-extractslator

Run long folder translation to zh.
Use supervisor + worker model for this batch run.
If platform supports subagents, supervisor must delegate bounded batches to worker subagents.
If subagents are unavailable, keep the same supervisor/worker contract in a single-agent loop.
```

Operational note:

- MCP mode does not expose a single `translate-batch` tool. Batch behavior is achieved by your agent looping over files and invoking MCP tools file-by-file.
- Multi-language output is also an agent loop pattern: run `translate` once per target language.

Design note:

1. Long-running multi-file orchestration uses centralized queue state under the temp root.
2. Queue state is designed for resume-safe small-batch processing.
3. Typical completion behavior is run-to-completion until queue is empty or only blocked items remain.
4. Batch processing uses supervisor/worker model; when platform supports subagents, delegation is required.
5. Canonical term definitions are maintained in `docs/README.md` under `Terminology Glossary`.

## Verified E2E Run (sample SRT)

Execution graph from the verified sample run (`samples/demo.en.srt`, English to Chinese, SRT input, CLI mode). The run ended at `state.done` with status `succeeded`.

<details>
<summary>Execution graph (Mermaid)</summary>

```mermaid

flowchart TD
    subgraph phase_01_intake_and_routing["01 Intake and routing"]
    state.ask_execution_profile["🚧 CK04 - Ask Execution Profile"]
    state.execution_profile_gate["❓ CK03 - Check Execution Profile"]
    state.read_localpaths["🔎 CK02 - Read Local Paths"]
    state.route_request_kind["❓ CK05 - Route Request Kind"]
    state.start["🔎 CK01 - Start"]
    end
    subgraph phase_02_batch_queue["02 Batch queue"]
    state.batch_apply_deltas["🔎 CK12 - Apply Worker Deltas"]
    state.batch_build_queue["🔎 CK07 - Build Or Reconcile Queue"]
    state.batch_cooldown_check["❓ CK13 - Check Cooldown Or Resume"]
    state.batch_dispatch_check["❓ CK08 - Check Pending Batch Work"]
    state.batch_init["🔎 CK06 - Initialize Batch Run"]
    state.batch_inline_worker["🔎 CK11 - Run Inline Worker"]
    state.batch_select_worker["❓ CK - Choose Worker Route"]
    state.batch_subagent_worker["🔎 CK10 - Run Worker Subagent"]
    state.batch_wait_resume["📜 CK14 - Wait For Resume"]
    state.emit_batch_summary["🔎 CK15 - Emit Batch Summary"]
    end
    subgraph phase_03_mode__mcp__and_cli_runtime["03 Mode, MCP, and CLI runtime"]
    state.apply_mcp_setup["🔎 CK20 - Apply MCP Setup"]
    state.ask_mcp_setup["🚧 CK19 - Ask MCP Setup Permission"]
    state.check_mode["❓ CK16 - Check Operating Mode"]
    state.ensure_cli_runtime["🔎 CK63 - Restore Native CLI Runtime"]
    state.mcp_preflight["🔎 CK17 - Validate MCP Prerequisites"]
    state.mcp_setup_check["❓ CK18 - Check MCP Setup"]
    end
    subgraph phase_04_media_and_ffmpeg_checks["04 Media and FFmpeg checks"]
    state.apply_ffmpeg_path["🔎 CK25 - Apply FFmpeg Path"]
    state.ask_ffmpeg_path["🚧 CK24 - Ask FFmpeg Path"]
    state.ffmpeg_path_check["❓ CK23 - Check FFmpeg Readiness"]
    state.ffmpeg_preflight["🔎 CK22 - Validate FFmpeg Path"]
    state.probe_media["🔎 CK26 - Probe Media"]
    state.single_check_input_kind["❓ CK21 - Check Input Kind"]
    state.target_track_check["❓ CK27 - Check Existing Target Subtitle"]
    end
    subgraph phase_05_opensubtitles_search_and_download["05 OpenSubtitles search and download"]
    state.adoption_decision["❓ CK35 - Route Adoption Decision"]
    state.advance_candidate_rank["🔎 CK42 - Advance Candidate Rank"]
    state.ask_candidate_adoption["🚧 CK34 - Ask Candidate Adoption"]
    state.ask_opensubtitles_auth_relogin["🚧 CK31 - Request OpenSubtitles Relogin"]
    state.cli_download_candidate["🔎 CK38 - Download Candidate Via CLI"]
    state.downloaded_language_check["❓ CK43 - Check Downloaded Subtitle Language"]
    state.mcp_download_candidate["🔎 CK37 - Download Candidate Via MCP"]
    state.opensubtitles_auth_check["❓ CK30 - Check OpenSubtitles Auth"]
    state.opensubtitles_candidate_check["❓ CK33 - Check OpenSubtitles Candidates"]
    state.opensubtitles_download_route["❓ CK36 - Route Download Execution"]
    state.opensubtitles_search["🔎 CK32 - Search OpenSubtitles"]
    state.opensubtitles_timing_gate["❓ CK39 - Check Whether Timing Validation Is Required"]
    state.subtitle_timing_check["🔎 CK40 - Run Subtitle Timing Check"]
    state.timing_check_result["❓ CK41 - Evaluate Timing Result"]
    state.validate_opensubtitles_auth["🔎 CK29 - Validate OpenSubtitles Auth"]
    state.write_downloaded_output["🔎 CK44 - Accept Downloaded Target Subtitle"]
    end
    subgraph phase_06_embedded_and_bitmap_sources["06 Embedded and bitmap sources"]
    state.cli_extract_source["🔎 CK52 - Extract Source Via CLI"]
    state.decode_bitmap_subtitle_frames["🔎 CK48 - Decode Bitmap Subtitle Frames"]
    state.export_bitmap_subtitle_stream["🔎 CK47 - Export Bitmap Subtitle Stream"]
    state.local_extract_route["❓ CK50 - Route Local Extraction"]
    state.mcp_extract_source["🔎 CK51 - Extract Source Via MCP"]
    state.ocr_bitmap_subtitle_frames["🔎 CK49 - OCR Bitmap Subtitle Frames"]
    state.select_source_subtitle_track["🔎 CK45 - Select Embedded Source Track"]
    state.source_subtitle_kind_check["❓ CK46 - Check Embedded Source Subtitle Type"]
    end
    subgraph phase_07_translation["07 Translation"]
    state.group_translation_input["🔎 CK53 - Build Translation Groups"]
    state.mcp_translate["🔎 CK56 - Translate Via MCP"]
    state.merge_translation["🔎 CK58 - Merge And Write Final SRT"]
    state.model_translate["🔎 CK57 - Translate Via Model Reasoning"]
    state.route_translation_engine["❓ CK55 - Route Translation Engine"]
    state.update_translation_memory["🔎 CK54 - Update Rolling Translation Memory"]
    end
    subgraph phase_08_delivery_and_completion["08 Delivery and completion"]
    state.done["✅ CK62 - Done"]
    state.emit_single_summary["🔎 CK61 - Emit Single-Run Summary"]
    state.mux_check["❓ CK59 - Check Optional Mux Output"]
    state.mux_output["🔎 CK60 - Mux Subtitle Into Media"]
    end
    state.adoption_decision -->|Adopt Candidate| state.opensubtitles_download_route
    state.adoption_decision -->|Reject Candidate| state.select_source_subtitle_track
    state.advance_candidate_rank -->|Advance Candidate Rank| state.opensubtitles_download_route
    state.apply_ffmpeg_path -->|Apply FFmpeg Path| state.probe_media
    state.apply_mcp_setup -->|Apply MCP Setup| state.single_check_input_kind
    state.ask_candidate_adoption -->|Ask To Adopt Candidate| state.adoption_decision
    state.ask_execution_profile -->|Collect Execution Profile| state.route_request_kind
    state.ask_ffmpeg_path -->|Ask For FFmpeg Path| state.apply_ffmpeg_path
    state.ask_mcp_setup -->|Ask To Configure MCP| state.apply_mcp_setup
    state.ask_opensubtitles_auth_relogin -->|Request OpenSubtitles Relogin| state.validate_opensubtitles_auth
    state.batch_apply_deltas -->|Apply Worker Deltas| state.batch_cooldown_check
    state.batch_build_queue -->|Build Or Reconcile Queue| state.batch_dispatch_check
    state.batch_cooldown_check -->|Need Cooldown| state.batch_wait_resume
    state.batch_cooldown_check -->|Continue Batch| state.batch_dispatch_check
    state.batch_dispatch_check -->|Batch Queue Empty| state.emit_batch_summary
    state.batch_dispatch_check -->|Batch Has Pending Work| state.batch_select_worker
    state.batch_init -->|Initialize Batch Workspace| state.batch_build_queue
    state.batch_inline_worker -->|Run Inline Worker| state.batch_apply_deltas
    state.batch_select_worker -->|Use Worker Subagent| state.batch_subagent_worker
    state.batch_select_worker -->|Use Inline Worker| state.batch_inline_worker
    state.batch_subagent_worker -->|Delegate Worker Batch| state.batch_apply_deltas
    state.batch_wait_resume -->|Wait For Cooldown Or Resume Signal| state.batch_dispatch_check
    state.check_mode -->|CLI Mode| state.single_check_input_kind
    state.check_mode -->|MCP Mode| state.mcp_preflight
    state.cli_download_candidate -->|Download Candidate Through CLI| state.opensubtitles_timing_gate
    state.cli_extract_source -->|Extract Source Through CLI| state.group_translation_input
    state.decode_bitmap_subtitle_frames -->|Decode Bitmap Subtitle Frames| state.ocr_bitmap_subtitle_frames
    state.downloaded_language_check -->|Downloaded Subtitle Already Target Language| state.write_downloaded_output
    state.downloaded_language_check -->|Downloaded Subtitle Needs Translation| state.group_translation_input
    state.emit_batch_summary -->|Emit Batch Summary| state.done
    state.emit_single_summary -->|Emit Single Summary| state.done
    state.ensure_cli_runtime -->|Restore Native CLI Runtime| state.read_localpaths
    state.execution_profile_gate -->|Missing Request Kind| state.ask_execution_profile
    state.execution_profile_gate -->|Missing Mode| state.ask_execution_profile
    state.execution_profile_gate -->|Missing Target Language| state.ask_execution_profile
    state.execution_profile_gate -->|Execution Profile Ready| state.route_request_kind
    state.export_bitmap_subtitle_stream -->|Export Bitmap Subtitle Stream| state.decode_bitmap_subtitle_frames
    state.ffmpeg_path_check -->|FFmpeg Ready| state.probe_media
    state.ffmpeg_path_check -->|FFmpeg Missing| state.ask_ffmpeg_path
    state.ffmpeg_preflight -->|Validate FFmpeg Path| state.ffmpeg_path_check
    state.group_translation_input -->|Build Translation Groups| state.update_translation_memory
    state.local_extract_route -->|Extract Via MCP| state.mcp_extract_source
    state.local_extract_route -->|Extract Via CLI| state.cli_extract_source
    state.mcp_download_candidate -->|Download Candidate Through MCP| state.opensubtitles_timing_gate
    state.mcp_extract_source -->|Extract Source Through MCP| state.group_translation_input
    state.mcp_preflight -->|Validate MCP Prerequisites| state.mcp_setup_check
    state.mcp_setup_check -->|MCP Ready| state.single_check_input_kind
    state.mcp_setup_check -->|MCP Needs Setup| state.ask_mcp_setup
    state.mcp_translate -->|Translate Through MCP| state.merge_translation
    state.merge_translation -->|Merge Translated Groups| state.mux_check
    state.model_translate -->|Translate Through Model Reasoning| state.merge_translation
    state.mux_check -->|Mux Requested| state.mux_output
    state.mux_check -->|No Mux Requested| state.emit_single_summary
    state.mux_output -->|Mux Subtitle Into Media| state.emit_single_summary
    state.ocr_bitmap_subtitle_frames -->|OCR Bitmap Subtitle Frames| state.group_translation_input
    state.opensubtitles_auth_check -->|OpenSubtitles Auth Ready| state.opensubtitles_search
    state.opensubtitles_auth_check -->|OpenSubtitles Relogin Required| state.ask_opensubtitles_auth_relogin
    state.opensubtitles_candidate_check -->|Candidates Found| state.ask_candidate_adoption
    state.opensubtitles_candidate_check -->|No Candidates| state.select_source_subtitle_track
    state.opensubtitles_download_route -->|Download Via MCP| state.mcp_download_candidate
    state.opensubtitles_download_route -->|Download Via CLI| state.cli_download_candidate
    state.opensubtitles_search -->|Search OpenSubtitles| state.opensubtitles_candidate_check
    state.opensubtitles_timing_gate -->|Need Timing Check| state.subtitle_timing_check
    state.opensubtitles_timing_gate -->|Skip Timing Check| state.downloaded_language_check
    state.probe_media -->|Probe Media| state.target_track_check
    state.read_localpaths -->|Read Local Paths| state.execution_profile_gate
    state.route_request_kind -->|Single Request| state.check_mode
    state.route_request_kind -->|Batch Request| state.batch_init
    state.route_translation_engine -->|Translate Via MCP| state.mcp_translate
    state.route_translation_engine -->|Translate Via Model| state.model_translate
    state.select_source_subtitle_track -->|Select Embedded Source Track| state.source_subtitle_kind_check
    state.single_check_input_kind -->|Input Is SRT| state.group_translation_input
    state.single_check_input_kind -->|Input Is Media| state.ffmpeg_preflight
    state.source_subtitle_kind_check -->|Bitmap Subtitle Track| state.export_bitmap_subtitle_stream
    state.source_subtitle_kind_check -->|Text Subtitle Track| state.local_extract_route
    state.start -->|Normalize Request| state.ensure_cli_runtime
    state.subtitle_timing_check -->|Run Subtitle Timing Check| state.timing_check_result
    state.target_track_check -->|Target Track Exists| state.emit_single_summary
    state.target_track_check -->|Target Track Missing| state.validate_opensubtitles_auth
    state.timing_check_result -->|Timing Acceptable| state.downloaded_language_check
    state.timing_check_result -->|Timing Rejected| state.advance_candidate_rank
    state.update_translation_memory -->|Update Rolling Translation Memory| state.route_translation_engine
    state.validate_opensubtitles_auth -->|Validate OpenSubtitles Auth| state.opensubtitles_auth_check
    state.write_downloaded_output -->|Accept Downloaded Target Subtitle| state.emit_single_summary
    style state.adoption_decision fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.advance_candidate_rank fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.apply_ffmpeg_path fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.apply_mcp_setup fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.ask_candidate_adoption fill:#fee2e2,stroke:#dc2626,color:#7f1d1d,stroke-width:1px
    style state.ask_execution_profile fill:#fee2e2,stroke:#dc2626,color:#7f1d1d,stroke-width:1px
    style state.ask_ffmpeg_path fill:#fee2e2,stroke:#dc2626,color:#7f1d1d,stroke-width:1px
    style state.ask_mcp_setup fill:#fee2e2,stroke:#dc2626,color:#7f1d1d,stroke-width:1px
    style state.ask_opensubtitles_auth_relogin fill:#fee2e2,stroke:#dc2626,color:#7f1d1d,stroke-width:1px
    style state.batch_apply_deltas fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.batch_build_queue fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.batch_cooldown_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.batch_dispatch_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.batch_init fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.batch_inline_worker fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.batch_select_worker fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.batch_subagent_worker fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.batch_wait_resume fill:#f8fafc,stroke:#94a3b8,color:#334155,stroke-width:1px
    style state.check_mode fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.cli_download_candidate fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.cli_extract_source fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.decode_bitmap_subtitle_frames fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.done fill:#dcfce7,stroke:#15803d,color:#14532d,stroke-width:1px
    style state.downloaded_language_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.emit_batch_summary fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.emit_single_summary fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.ensure_cli_runtime fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.execution_profile_gate fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.export_bitmap_subtitle_stream fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.ffmpeg_path_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.ffmpeg_preflight fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.group_translation_input fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.local_extract_route fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.mcp_download_candidate fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.mcp_extract_source fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.mcp_preflight fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.mcp_setup_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.mcp_translate fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.merge_translation fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.model_translate fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.mux_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.mux_output fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.ocr_bitmap_subtitle_frames fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.opensubtitles_auth_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.opensubtitles_candidate_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.opensubtitles_download_route fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.opensubtitles_search fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.opensubtitles_timing_gate fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.probe_media fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.read_localpaths fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.route_request_kind fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.route_translation_engine fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.select_source_subtitle_track fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.single_check_input_kind fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.source_subtitle_kind_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.start fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.subtitle_timing_check fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.target_track_check fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.timing_check_result fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
    style state.update_translation_memory fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.validate_opensubtitles_auth fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.write_downloaded_output fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
    style state.done stroke:#ea580c,stroke-width:3px
    subgraph legend[Legend]
        legend_ai["🔎 AI"]
    style legend_ai fill:#dcfce7,stroke:#16a34a,color:#14532d,stroke-width:1px
        legend_tool["⚙️ Code/Tool"]
    style legend_tool fill:#dbeafe,stroke:#2563eb,color:#1e3a8a,stroke-width:1px
        legend_branch["❓ Conditional branch"]
    style legend_branch fill:#fef3c7,stroke:#a16207,color:#713f12,stroke-width:1px
        legend_optional["💬 Optional user choice"]
    style legend_optional fill:#fef3c7,stroke:#d97706,color:#78350f,stroke-width:1px
        legend_required["🚧 Required user input"]
    style legend_required fill:#fee2e2,stroke:#dc2626,color:#7f1d1d,stroke-width:1px
        legend_gate["📜 Gate"]
    style legend_gate fill:#f8fafc,stroke:#94a3b8,color:#334155,stroke-width:1px
        legend_completion["✅ Completion"]
    style legend_completion fill:#dcfce7,stroke:#15803d,color:#14532d,stroke-width:1px
    end

```

</details>

## What The Skill Solves

- Provides a reusable subtitle workflow skill contract for probe/search/extract/translate/merge.
- Keeps cue order and timestamps stable while translating text content.
- Standardizes agent-side execution through MCP tools.
- Provides CLI runtime knobs for grouping, batch sizing, retries, and model endpoint settings.

## Current implementation scope

Execution modes:

- CLI mode (default)
- MCP stdio mode (`--mode mcp`)

Workflow steps:

1. Probe media subtitle tracks for target language.
2. Query OpenSubtitles candidates (real API when configured; mock fallback optional).
3. Extract local subtitle (prefer English, fallback nearest available).
4. Group cues by timeline rules.
5. Build rolling scene summary and historical context.
6. Translate by mode policy.
7. Merge and emit SRT.
8. Optional: remux generated AI subtitle into source media as a new subtitle language track.

Translation policy:

- MCP mode: sampling-only (`sampling/createMessage`). Sampling failures return errors.
- CLI mode: external provider only (including custom endpoint access).

## Build

```powershell
dotnet build SubtitleExtractslator.sln
```

```bash
dotnet build SubtitleExtractslator.sln
```

## Project structure

- `subtitle-extractslator/`: skill package (primary)
- `SubtitleExtractslator.Cli/`: skill runtime host (CLI + MCP tools + workflow core)
- `docs/`: setup and operational notes
- `samples/`: sample SRT and trace files

## CLI usage

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode cli probe --input "movie.mkv" --lang zh

dotnet run --project SubtitleExtractslator.Cli -- --mode cli subtitle-timing-check --input "movie.mkv" --subtitle "movie.zh.srt"

dotnet run --project SubtitleExtractslator.Cli -- --mode cli opensubtitles-search --input "movie.mkv" --lang zh --search-query-primary "movie" --search-query-normalized "movie s00e00"

dotnet run --project SubtitleExtractslator.Cli -- --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en

dotnet run --project SubtitleExtractslator.Cli -- --mode cli translate --input "movie.en.srt" --lang zh --output "movie.zh.srt"

dotnet run --project SubtitleExtractslator.Cli -- --mode cli translate-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"
```

```bash
dotnet run --project SubtitleExtractslator.Cli -- --mode cli probe --input "movie.mkv" --lang zh

dotnet run --project SubtitleExtractslator.Cli -- --mode cli subtitle-timing-check --input "movie.mkv" --subtitle "movie.zh.srt"

dotnet run --project SubtitleExtractslator.Cli -- --mode cli opensubtitles-search --input "movie.mkv" --lang zh --search-query-primary "movie" --search-query-normalized "movie s00e00"

dotnet run --project SubtitleExtractslator.Cli -- --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en

dotnet run --project SubtitleExtractslator.Cli -- --mode cli translate --input "movie.en.srt" --lang zh --output "movie.zh.srt"

dotnet run --project SubtitleExtractslator.Cli -- --mode cli translate-batch --input-list "./inputs.txt" --lang zh --output-dir "./out" --output-suffix ".zh.srt"
```

Batch input file format (`--input-list`):

- UTF-8 text file.
- One media/subtitle path per line.
- Empty lines and lines starting with `#` are ignored.

Batch mode is CLI-only. MCP mode intentionally does not provide batch workflow due to common timeout constraints in MCP clients.

CLI common options:

- `--env "KEY=VALUE;KEY2=VALUE2"` injects temporary environment overrides for the current command only.
- `--help` prints complete command help.

## MCP stdio mode

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode mcp
```

```bash
dotnet run --project SubtitleExtractslator.Cli -- --mode mcp
```

MCP transport and tool registration use the official `ModelContextProtocol` NuGet package (`AddMcpServer().WithStdioServerTransport().WithTools<...>()`).

The MCP server supports:

- `probe`
- `subtitle_timing_check`
- `opensubtitles_search`
- `opensubtitles_download`
- `extract`
- `translate`

MCP tool return contract:

- Tools return a structured object with `ok`, `data`, and `error`.
- On success: `ok=true`, `data` contains tool result.
- On failure: `ok=false`, `error` includes `code`, `message`, optional `snapshotPath`, and `timeUtc`.

## Translation providers

- MCP sampling provider uses official MCP sampling (`sampling/createMessage`).
- MCP sampling retries follow `LLM_RETRY_COUNT` (or overrides).
- Oversized responses trigger a concise-reasoning warning in the next retry.
- MCP has no external fallback on translation errors.
- External/custom endpoint access is CLI route only.

## OpenSubtitles

- Real API search/download requires local auth cache from `subtitle auth login`.
- `subtitle auth login` stores api key, username, and password in local cache for later `aquire` usage.
- Optional mock branch remains available via `OPENSUBTITLES_MOCK=1` for offline testing.

## Build a RID NativeAOT package

```bash
pwsh -NoProfile -File ./scripts/pack-rid-runtime.ps1 -Rid linux-arm64 -PackageVersion 0.1.0 -OutputRoot artifacts
```

The release workflows build the supported RID matrix, validate the complete version set, and publish the resulting NuGet packages with SHA-512 sidecars. `linux-arm` is not currently included in the NativeAOT package set.
