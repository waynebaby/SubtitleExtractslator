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

If the downloaded skill package is missing runtime binaries in your environment, use the release links below and install from the platform zip directly.

<!-- release-links:start -->
- Latest releases: [Releases](https://github.com/waynebaby/SubtitleExtractslator/releases)
- Windows x64 package (v0.1.10): [subtitle-extractslator-v0.1.10-win-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-win-x64.zip)
- Windows ARM64 package (v0.1.10): [subtitle-extractslator-v0.1.10-win-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-win-arm64.zip)
- Linux x64 package (v0.1.10): [subtitle-extractslator-v0.1.10-linux-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-linux-x64.zip)
- Linux musl x64 package (v0.1.10): [subtitle-extractslator-v0.1.10-linux-musl-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-linux-musl-x64.zip)
- Linux ARM64 package (v0.1.10): [subtitle-extractslator-v0.1.10-linux-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-linux-arm64.zip)
- Linux musl ARM64 package (v0.1.10): [subtitle-extractslator-v0.1.10-linux-musl-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-linux-musl-arm64.zip)
- Linux ARM package (v0.1.10): [subtitle-extractslator-v0.1.10-linux-arm.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-linux-arm.zip)
- macOS ARM64 package (v0.1.10): [subtitle-extractslator-v0.1.10-osx-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-osx-arm64.zip)
- macOS x64 package (v0.1.10): [subtitle-extractslator-v0.1.10-osx-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.10/subtitle-extractslator-v0.1.10-osx-x64.zip)
<!-- release-links:end -->

## First: Use The ZIP In Your Agent

If your goal is to run this as a skill in your own agent, start here.

1. Download the platform zip from the release links above.
2. Unzip it and keep the folder name as `subtitle-extractslator`.
3. Install that folder as a local skill package in your agent client (for example, Claude Desktop skills).
4. Verify runtime assets exist under `subtitle-extractslator/assets/bin/<rid>/` for your OS.
5. Run one simple operation (for example `probe` or `translate`) to confirm the skill is callable.

Notes:
- This repository's primary deliverable is the `subtitle-extractslator/` skill package inside the zip.
- The `SubtitleExtractslator.Cli/` project is the runtime host used by the skill (CLI + MCP server).
- Build and packaging details are in `docs/skill-installation-and-build.md`.

### Usage scenarios

Use the skill name in your agent chat:

```text
/subtitle-extractslator
```

Scenario 1: Process all videos under a folder (MCP mode)

```text
/subtitle-extractslator

Run in MCP mode.
Process all video files under <media_root> recursively (for example /data/media or D:\\media).
For each file: probe -> extract preferred English subtitle -> translate to zh -> write output next to source.
If a file already has a .zh.srt sibling, skip it.
Return a final table with success/failure for each file.
```

Scenario 2: Translate one SRT file to a target language

```text
/subtitle-extractslator

Translate a single subtitle file.
Input: <media_root>/episode01.en.srt
Target language: ja
Output: <media_root>/episode01.ja.srt
Keep timeline and cue numbering unchanged.
```

Scenario 3: Translate one SRT file to multiple languages

```text
/subtitle-extractslator

Run in MCP mode.
Input subtitle: <media_root>/episode01.en.srt
Target languages: zh, ja, es
Generate one output per language in the same directory:
- episode01.zh.srt
- episode01.ja.srt
- episode01.es.srt
Keep timeline and cue numbering unchanged for every output.
```

Operational note:
- MCP mode does not expose a single `translate-batch` tool. Batch behavior is achieved by your agent looping over files and invoking MCP tools file-by-file.
- Multi-language output is also an agent loop pattern: run `translate` once per target language.

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











