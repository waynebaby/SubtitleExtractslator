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
  6. Translate by policy (MCP: sampling -> external fallback; CLI: external only).
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

### Parameter Override Rule

Configuration precedence is:

1. Command/tool parameter override
2. Environment override parameter (`--env` in CLI or `envOverrides` in MCP `run_workflow`)
3. Process environment variable
4. Built-in default

CLI environment override format:

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt" --llm-retry-count 5 --env "LLM_MODEL=qwen/qwen3.5-35b-a3b;LLM_ENDPOINT=http://localhost:1234/api/v1/chat"
```

MCP `run_workflow` supports `envOverrides` using the same `KEY=VALUE;KEY2=VALUE2` format.

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

- Sampling provider is currently a stub that returns unavailable.
- External provider defaults to no-op (preserves original text).
- Set environment variable `TRANSLATION_MODE=prefix` to simulate translated output.

### Strong Model Recommendation

For films, TV scripts, stand-up lines, slang, dark humor, sexual jokes, and culturally sensitive references, strongly prefer an uncensored model variant.

Reason:

1. Literary and dramatic dialogue often relies on taboo, sarcasm, and punchline timing.
2. Over-filtered models are more likely to soften, skip, or partially copy source lines instead of translating faithfully.
3. Uncensored variants generally preserve tone, intent, and comedic effect better.

Recommended practice:

1. Set `LLM_MODEL` to an uncensored-capable model variant when translating entertainment content.
2. Keep existing structure constraints enabled so stronger language understanding does not break SRT format.

## OpenSubtitles

- Current implementation includes a mock branch controlled by `OPENSUBTITLES_MOCK`.
- Real API integration should be added in a dedicated provider module with auth and rate-limit handling.

## Publish single-file examples

```powershell
dotnet publish SubtitleExtractslator.Cli -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true

dotnet publish SubtitleExtractslator.Cli -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true

dotnet publish SubtitleExtractslator.Cli -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true
```

## CI publish matrix (win/linux/macos)

The repository includes GitHub Actions workflow `.github/workflows/publish-single-file-matrix.yml`.

It publishes single-file self-contained binaries for:

- `win-x64`
- `linux-x64`
- `osx-arm64`

Trigger options:

- Manual: run `Publish Single-File Matrix` from GitHub Actions UI.
- Tag: push tag like `v1.0.0`.

Each matrix job uploads artifact `subtitle-extractslator-<rid>`.
