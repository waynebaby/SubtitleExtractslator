# SubtitleExtractslator

A .NET single-file CLI with optional MCP stdio mode for subtitle probing, extraction, grouping, rolling-context translation, and SRT merge output.

## Current implementation scope

- Dual mode startup in one executable:
  - CLI mode (default)
  - MCP stdio mode (`--mode mcp`)
- Workflow routing:
  1. Probe media subtitle tracks for target language.
  2. Query OpenSubtitles candidates (mocked unless configured).
  3. Extract local subtitle (prefer English, fallback nearest available).
  4. Group cues by timeline rules.
  5. Build rolling scene summary + historical knowledge state.
  6. Translate by policy (MCP: sampling-only, fail on sampling errors; CLI: external provider only).
  7. Merge and emit SRT.

## Build

```powershell
dotnet build SubtitleExtractslator.sln
```

## CLI usage

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode cli probe --input "movie.mkv" --lang zh

dotnet run --project SubtitleExtractslator.Cli -- --mode cli opensubtitles-search --input "movie.mkv" --lang zh

dotnet run --project SubtitleExtractslator.Cli -- --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en

dotnet run --project SubtitleExtractslator.Cli -- --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt"
```

## MCP stdio mode

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode mcp
```

MCP transport and tool registration use the official `ModelContextProtocol` NuGet package (`AddMcpServer().WithStdioServerTransport().WithTools<...>()`).

The MCP server supports:

- `probe`
- `opensubtitles_search`
- `extract`
- `run_workflow`

## Translation providers

- MCP sampling provider uses official MCP sampling (`sampling/createMessage`) with retry behavior aligned to `LLM_RETRY_COUNT`.
- In MCP mode, oversized sampling responses trigger a concise-reasoning warning on next retry to reduce overthinking output.
- In MCP mode, sampling is required; there is no external fallback on sampling failures.
- External/custom endpoint access is CLI route only.

## OpenSubtitles

- Current implementation includes a mock branch controlled by `OPENSUBTITLES_MOCK`.
- Real API integration should be added in a dedicated provider module with auth and rate-limit handling.

## Publish single-file examples

```powershell
dotnet publish SubtitleExtractslator.Cli -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true

dotnet publish SubtitleExtractslator.Cli -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true

dotnet publish SubtitleExtractslator.Cli -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true
```
