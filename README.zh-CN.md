# Subtitle Extractslator（中文）

[English README](README.md)

SubtitleExtractslator 是一个以 skill 为主体的字幕翻译项目。

这个仓库的核心交付物是 skill 包（提示词、策略、运行资产与使用约定）。CLI 与 MCP 服务端是 skill 的运行时实现层，用于在本地脚本和 agent 环境中稳定执行该 skill。

仓库中的运行形态：

- CLI（本地命令行自动化）
- MCP stdio 服务器（供 agent / MCP 客户端调用）

它覆盖从字幕探测到最终 SRT 输出的完整链路：探测已有字幕、检索候选、提取源字幕、按上下文分组翻译、合并并输出结果，同时尽量保持时间轴和结构稳定。

## 下载

从 GitHub 仓库安装 skill 源码：

```bash
npx skills add waynebaby/SubtitleExtractslator --skill subtitle-extractslator
```

CLI 运行时以每 RID 独立的 NativeAOT NuGet 包发布。Stable 和 Beta Release 均不发布 skill ZIP。执行 CLI/MCP 或字幕操作前，skill bootstrap 会检查当前通道最新包，并按需恢复本机 RID 包。

包索引：

- 稳定通道: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- 稳定通道中文: [packages.released.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md)
- Beta 通道: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Beta 通道中文: [packages.beta.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md)

Bootstrap 示例（从已安装的 skill 目录运行）：

```bash
python3 assets/bootstrap/restore_runtime.py --channel stable
```

Bootstrap 会输出绝对 native executable 路径；使用该路径运行 guide：

```bash
<absolute-path>/SubtitleExtractslator.Cli --guide
```

Native CLI 不需要 .NET Runtime。Bootstrap 需要 Python 3 和 NuGet HTTPS 访问；处理媒体时仍需 FFmpeg。包索引列出全部 RID 包 ID 和精确版本回退资产。

<!-- release-links:start -->
- 稳定通道包索引：[packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- 稳定通道中文包索引：[packages.released.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md)
- Beta 通道包索引：[packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Beta 通道中文包索引：[packages.beta.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md)
- 各 RID 回退 `.nupkg` 和 SHA-512 sidecar 由上述包索引页维护。
<!-- release-links:end -->

## 先走 Guide-First 入口

如果你要在 agent 中运行此 skill，请从仓库安装 skill，并以 bootstrap 恢复出的 NativeAOT 可执行文件作为 CLI/MCP 命令入口。

1. 从仓库安装 skill 源码。
2. 在 CLI/MCP 或字幕操作前运行 `assets/bootstrap/restore_runtime.py --channel <metadata.channel>`。
3. 使用 bootstrap 输出的绝对可执行文件路径运行 `--guide`。
4. NuGet 不可用时，使用所选通道回退 Release 中对应 RID/版本的 `.nupkg` 和 `.sha512` 资产。

说明：

- 仓库继续保留 `.github/skills/subtitle-extractslator/` 作为 skill 路由与策略层。
- `.github/skills/subtitle-extractslator/` skill 包本身不携带 `assets/bin/`，保持 binary-free。
- `SubtitleExtractslator.Cli/` 是运行时源码；每 RID NativeAOT NuGet 包提供 CLI + MCP 可执行文件。
- 对于 SO 增强后的 skill，真正的执行依据是 `.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json`；`skill-plan.md` 仅是 planner 输入。
- 构建与打包细节见 `docs/skill-installation-and-build.md`。

### 使用场景示例（短提示模板）

在支持 skill 的 agent 对话中可直接用：

```text
/subtitle-extractslator
```

场景 1：单个视频翻译到中文（指定本地模型端口）

```text
/subtitle-extractslator

把 D:\media\xxx.mkv 翻译成中文。
本地模型端口使用 http://127.0.0.1:1234/v1/chat/completions。
输出 D:\media\xxx.zh.srt。
```

场景 2：递归处理一个文件夹

```text
/subtitle-extractslator

请使用 MCP 模式。
把 D:\tv\Fallout\S01 递归翻译到中文。
已有 .zh.srt 的文件直接跳过。
```

场景 3：从中断点继续整个目录任务

```text
/subtitle-extractslator

继续上次 D:\tv\Fallout 的中文任务。
从集中临时队列状态恢复，不要重跑已完成项。
```

场景 4：单个 SRT 输出多语言

```text
/subtitle-extractslator

把 D:\subs\episode01.en.srt 翻译为 zh、ja、es。
保持时间轴和 cue 顺序不变。
```

场景 5：只做探测不翻译

```text
/subtitle-extractslator

探测 D:\media\xxx.mkv 是否有内嵌中文字幕轨。
只返回探测结果。
```

场景 6：Supervisor/Worker 处理批任务

```text
/subtitle-extractslator

执行目录级中文翻译长任务。
这类批处理统一使用 supervisor + worker 模型。
如果平台支持 subagent，supervisor 必须把 bounded 批次委派给 worker 子代理。
如果不支持，保持同样 supervisor/worker 合同在单代理循环中执行。
```

运行说明：

- MCP 模式不提供单个 `translate-batch` 工具。批处理由 agent 在目录层面自行循环，逐个文件调用 MCP tools 实现。
- 多语言输出同理：由 agent 按目标语言逐次调用 `translate`。

设计说明：

1. 多文件长任务的队列状态采用集中临时目录存放。
2. 队列状态支持小批次持续推进和中断恢复。
3. 默认是持续处理到队列清空，或仅剩真实阻塞项。
4. 批处理统一采用 supervisor/worker；平台支持 subagent 时，委派是必选项。
5. 术语标准定义统一维护在 `docs/README.md` 的 `Terminology Glossary` 小节。

## 已验证的 E2E 执行图（样例 SRT）

以下为已验证样例运行（`samples/demo.en.srt`，英译中，SRT 输入，CLI 模式）的执行图，运行结束于 `state.done`，状态为 `succeeded`。

<details>
<summary>执行图（Mermaid）</summary>

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

## 这个 Skill 解决什么问题

- 提供可复用的字幕工作流 skill 约定（probe/search/extract/translate/merge）。
- 在翻译过程中保持 cue 顺序与时间戳稳定。
- 通过 MCP tools 标准化 agent 侧调用方式。
- 通过 CLI 暴露分组、批量、重试、模型与端点等运行参数。

## 当前实现范围

运行模式：

- CLI（默认）
- MCP stdio（`--mode mcp`）

工作流步骤：

1. 探测媒体文件中的字幕轨道。
2. 查询 OpenSubtitles 候选（配置后走真实 API，未配置可走 mock）。
3. 本地提取字幕（优先英文，失败时做确定性回退）。
4. 按时间线规则分组 cues。
5. 构建滚动场景摘要与历史上下文。
6. 按模式策略执行翻译。
7. 合并并输出 SRT。
8. 可选：将生成的 AI 字幕回封装进原视频，作为新语言字幕轨道。

翻译策略：

- MCP 模式：仅 sampling（`sampling/createMessage`），sampling 失败直接报错。
- CLI 模式：仅 external provider（包含自定义 endpoint 访问）。

## 构建

```powershell
dotnet build SubtitleExtractslator.sln
```

```bash
dotnet build SubtitleExtractslator.sln
```

## 项目结构

- `subtitle-extractslator/`：skill 包（主体）
- `SubtitleExtractslator.Cli/`：skill 运行时宿主（CLI + MCP tools + workflow 核心）
- `docs/`：安装与运维文档
- `samples/`：示例字幕与 trace 文件

## CLI 用法

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

批量输入文件格式（`--input-list`）：

- UTF-8 文本文件。
- 每行一个媒体/字幕文件路径。
- 空行和以 `#` 开头的行会被忽略。

批量模式仅在 CLI 提供。MCP 模式不提供批量工作流，以避免 MCP 客户端常见的超时问题。

CLI 通用参数：

- `--env "KEY=VALUE;KEY2=VALUE2"`：仅对当前命令注入临时环境变量覆盖。
- `--help`：打印完整命令帮助。

## MCP stdio 模式

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode mcp
```

```bash
dotnet run --project SubtitleExtractslator.Cli -- --mode mcp
```

MCP 传输与工具注册使用官方 `ModelContextProtocol` NuGet 包（`AddMcpServer().WithStdioServerTransport().WithTools<...>()`）。

MCP tools：

- `probe`
- `subtitle_timing_check`
- `opensubtitles_search`
- `opensubtitles_download`
- `extract`
- `translate`

MCP 工具返回约定：

- 工具返回结构化对象：`ok`、`data`、`error`。
- 成功时：`ok=true`，`data` 为工具结果。
- 失败时：`ok=false`，`error` 包含 `code`、`message`、可选 `snapshotPath`、`timeUtc`。

## 翻译提供者说明

- MCP sampling 使用官方 `sampling/createMessage`。
- MCP sampling 重试次数遵循 `LLM_RETRY_COUNT`（或覆盖值）。
- 当响应过大时，下一次重试会注入“简化思考”提示以降低过度思考输出。
- MCP 模式翻译失败时不会回退到 external。
- external / 自定义 endpoint 访问仅走 CLI 路线。

## OpenSubtitles

- 真实 API 搜索/下载依赖 `subtitle auth login` 写入的本地认证缓存。
- `subtitle auth login` 会写入 api key、username、password，后续由 `aquire` 读取。
- 仍保留 `OPENSUBTITLES_MOCK=1` 的离线测试分支。
- 真实 API 集成建议拆分到独立 provider 模块，并补充鉴权与限流处理。

## 构建 RID NativeAOT 包

```bash
pwsh -NoProfile -File ./scripts/pack-rid-runtime.ps1 -Rid linux-arm64 -PackageVersion 0.1.0 -OutputRoot artifacts
```

Release workflow 会构建受支持 RID 矩阵、校验版本集完整性，并发布附带 SHA-512 sidecar 的 NuGet 包。`linux-arm` 当前不在 NativeAOT 包支持范围内。
