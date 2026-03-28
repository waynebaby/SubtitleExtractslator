# Implementation Map

This document maps concrete source files to runtime behavior and ownership boundaries.

## 1. Solution and project map

1. `SubtitleExtractslator.sln`
   - Aggregates CLI runtime project and test project.
2. `SubtitleExtractslator.Cli/`
   - Main runtime host and implementation.
3. `SubtitleExtractslator.Tests/`
   - Integration-style translation tests and guard assertions.

## 2. Entry and command surface

## 2.1 Process entry

- `SubtitleExtractslator.Cli/Program.cs`
  - Parses mode and arguments.
  - Configures runtime logging flags.
  - Starts MCP host in MCP mode.
  - Runs CLI command dispatch in CLI mode.
  - Captures unhandled exceptions to snapshot logs.

## 2.2 CLI argument and command handling

- `SubtitleExtractslator.Cli/AppOptions.cs`
  - Parses `--key value` options and positional command groups.
  - Defines user-visible help text and command matrix.

- `SubtitleExtractslator.Cli/CliCommandRunner.cs`
  - Routes parsed commands to orchestrator/auth store.
  - Validates required arguments and integer options.
  - Implements batch translation input-list handling.
  - Applies per-command temporary env overrides via scope.

- `SubtitleExtractslator.Cli/AppModels.cs`
  - Defines shared records/enums for command results and domain data.

## 3. Orchestration and media operations

## 3.1 Orchestrator

- `SubtitleExtractslator.Cli/WorkflowOrchestrator.cs`
  - Public operations:
    - `ProbeAsync`
    - `SearchOpenSubtitlesAsync`
    - `DownloadOpenSubtitleAsync`
    - `ExtractSubtitleAsync`
    - `TranslateAsync`
    - `RunWorkflowAsync` (full chain path)
  - Owns translation-unit splitting and parallel translation execution.
  - Owns per-workflow scopes for env overrides, retry overrides, health buckets, and history logs.

## 3.2 Media and subtitle operations

- `SubtitleExtractslator.Cli/SubtitleOperations.cs`
  - FFprobe subtitle track probing.
  - OpenSubtitles staged search and download orchestration.
  - Text subtitle extraction and bitmap extraction branch.
  - SRT read/write.
  - Subtitle mux into media container.
  - Search query normalization and fallback query generation.

- `SubtitleExtractslator.Cli/PgsSupDecoder.cs`
  - Built-in PGS SUP parser/decoder.
  - RLE decode + palette application.
  - PNG frame generation with cue timeline.

## 4. MCP tool surface

- `SubtitleExtractslator.Cli/McpTools.cs`
  - Registers and implements MCP tools:
    - `probe`
    - `opensubtitles_search`
    - `opensubtitles_download`
    - `extract`
    - `translate`
  - Converts exceptions to structured MCP failures.
  - Implements compact-response policy for MCP payloads.

## 5. OpenSubtitles stack

- `SubtitleExtractslator.Cli/OpenSubtitlesAuthStore.cs`
  - Auth cache persistence and lifecycle (`login/aquire/status/clear`).
  - Default endpoint/user-agent normalization.

- `SubtitleExtractslator.Cli/OpenSubtitlesAccessor.cs`
  - HTTP login/token management.
  - Search API calls with fallback and rerank.
  - Download-link resolution and file retrieval.
  - Rate-limit retry loop and sanitized HTTP debug block generation.

## 6. Runtime infrastructure and translation engine

The file `SubtitleExtractslator.Cli/RuntimeInfrastructure.cs` contains multiple internal modules.

## 6.1 Bootstrap and grouping

1. `FfmpegBootstrap`
   - Resolves or downloads FFmpeg binaries.
2. `GroupingEngine`
   - Deterministic fixed-size cue grouping and reindexing.

## 6.2 Translation pipeline and providers

1. `TranslationPipeline`
   - Builds context envelope and validates translated structure.
   - Chooses provider according to mode context.
2. `SamplingTranslationProvider`
   - MCP sampling translation with retry and oversize fallback logic.
3. `ExternalTranslationProvider`
   - HTTP LLM translation for CLI mode.
   - Supports OpenAI-like and Claude-like payload formats.
   - Supports key-based and Azure RBAC-style auth.

## 6.3 Context and runtime scopes

1. `McpSamplingRuntimeContext`
   - AsyncLocal scope for MCP server injection.
2. `LlmRuntimeOverrides`
   - AsyncLocal retry-count override scope.
3. `RuntimeEnvironmentOverrides`
   - Temporary process env override scope from command arguments.

## 6.4 Health, IO, and logs

1. `ResponseSizeHealthMonitor`
   - Rolling response-size guard window.
2. `SrtSerializer`
   - SRT parse/serialize implementation.
3. `RuntimePathPolicy`
   - temp/intermediate path strategy.
4. `ErrorSnapshotWriter`
   - structured diagnostics and markdown IO dumps.
5. `CliRuntimeLog`
   - runtime log sequencing and scoped timing.

## 7. Tests and verification

- `SubtitleExtractslator.Tests/TranslationBatchTests.cs`
  - Verifies translation output invariants on sample inputs.
  - Supports fake providers by default and optional real LLM mode via environment flag.
  - Ensures output width constraints and Chinese character presence in translated output.

## 8. Packaging and release automation

- `scripts/publish-skill-binaries.ps1`
  - Builds and publishes single-file binaries for multiple RIDs into skill assets.

- `.github/workflows/release-skill.yml`
  - Restore/build/test pipeline.
  - Semver bump logic from PR labels.
  - SKILL/README release-link updates.
  - Multi-platform binary packaging and release artifact creation.

## 9. Main extension points for maintainers

Common modification entry points:

1. Add a new CLI command:
   - `AppOptions.HelpText`
   - `CliCommandRunner.RunAsync`
   - possibly new orchestrator method/result model

2. Add a new MCP tool:
   - `McpTools.cs` tool method and description
   - orchestrator wiring

3. Change translation behavior:
   - context/prompt and parser logic in `TranslationPipeline` and `ExternalTranslationProvider`
   - retry/health behavior in `ResponseSizeHealthMonitor` and provider retry loops

4. Change OpenSubtitles behavior:
   - query and fallback strategy in `SubtitleOperations`
   - HTTP/auth details in `OpenSubtitlesAccessor` and `OpenSubtitlesAuthStore`

5. Change extraction for bitmap subtitles:
   - decode path in `PgsSupDecoder`
   - OCR branch in `SubtitleOperations`
