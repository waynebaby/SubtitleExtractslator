# Subtitle Extractslator

[English](README.md) | [中文](README.zh-CN.md)

SubtitleExtractslator is a subtitle translation skill project.

The primary deliverable in this repository is the skill package (prompts, policy, runtime assets, and usage contract). The .NET CLI and MCP server are runtime implementations that exist to execute this skill reliably in local scripts and agent environments.

Runtime forms in this repository:

- CLI application for local automation
- MCP stdio server for agent-driven workflows

It is built for end-to-end subtitle processing: detect existing tracks, search candidates, extract source subtitles, translate with context-aware batching, and emit final SRT output while preserving subtitle timing and structure.

## Downloads

Quick install skill command:

```bash
npx skills add waynebaby/SubtitleExtractslator
```

Primary runtime distribution now uses the `SubtitleExtractslator.Cli` NuGet package plus a portable DLL entry outside the skill folder.

Package indexes:

- Stable index: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- Stable index (zh-CN): [packages.released.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md)
- Beta index: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Beta index (zh-CN): [packages.beta.zh-CN.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md)

Install example:

```bash
dotnet add package SubtitleExtractslator.Cli --version <VERSION>
```

Guide-first entry:

```bash
dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide
```

Use the package index pages above as the canonical acquisition surface. If package feed is unavailable, use the fallback `.nupkg` link maintained inside the selected package index page.

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
- Runtime fallback .nupkg links are maintained in the package index pages above.
<!-- release-links:end -->

## First: Guide-First Runtime Entry

If your goal is to run this as a skill in your own agent, keep `npx skills add` for discovery, and use NuGet package runtime as command source of truth.

1. Install package from stable/beta channel.
2. Resolve an absolute DLL path from the restored or extracted package.
3. Run `dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide` first.
4. Follow guide command entries for CLI or MCP mode.
5. If package feed is unavailable, use the fallback `.nupkg` link listed inside the selected package index page.

Notes:

- This repository keeps `.github/skills/subtitle-extractslator/` for skill routing and policy context.
- The `.github/skills/subtitle-extractslator/` skill package is binary-free and does not ship `assets/bin/`.
- The `SubtitleExtractslator.Cli/` project is the runtime host used by the skill (CLI + MCP server), delivered as a separate portable DLL package.
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

## Publish single-file examples

```powershell
dotnet publish SubtitleExtractslator.Cli -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true

dotnet publish SubtitleExtractslator.Cli -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true

dotnet publish SubtitleExtractslator.Cli -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true
```

```bash
dotnet publish SubtitleExtractslator.Cli -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true
dotnet publish SubtitleExtractslator.Cli -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true
```


