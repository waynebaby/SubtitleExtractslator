# Development and Operations

## 1. Local development workflow

Repository-level build and test:

```powershell
dotnet restore SubtitleExtractslator.sln
dotnet build SubtitleExtractslator.sln -c Debug
dotnet test SubtitleExtractslator.sln -c Debug
```

```bash
dotnet restore SubtitleExtractslator.sln
dotnet build SubtitleExtractslator.sln -c Debug
dotnet test SubtitleExtractslator.sln -c Debug
```

CLI run examples:

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode cli probe --input "samples/demo.en.srt" --lang zh
dotnet run --project SubtitleExtractslator.Cli -- --mode cli translate --input "samples/demo.en.srt" --lang zh --output "artifacts/test-output/demo.zh.srt"
```

```bash
dotnet run --project SubtitleExtractslator.Cli -- --mode cli probe --input "samples/demo.en.srt" --lang zh
dotnet run --project SubtitleExtractslator.Cli -- --mode cli translate --input "samples/demo.en.srt" --lang zh --output "artifacts/test-output/demo.zh.srt"
```

MCP server run:

```powershell
dotnet run --project SubtitleExtractslator.Cli -- --mode mcp
```

```bash
dotnet run --project SubtitleExtractslator.Cli -- --mode mcp
```

## 2. Binary publishing and packaging

Local multi-platform publish helper:

```powershell
.\scripts\publish-skill-binaries.ps1
```

```bash
pwsh ./scripts/publish-skill-binaries.ps1
```

Default output root:

- `subtitle-extractslator/assets/bin/`

CI release workflow:

- `.github/workflows/release-skill.yml`

Release workflow covers:

1. restore/build/test
2. version bump selection
3. release-link updates in README files
4. multi-platform binary packaging
5. release asset publication

## 3. Runtime configuration matrix

The following variables are read by the runtime implementation.

## 3.1 Logging and workflow controls

| Variable | Default | Purpose |
| --- | --- | --- |
| `SUBTITLEEXTRACTSLATOR_CLI_LOG` | `true` (effective unless `--quiet`) | Enable/disable CLI runtime logs. |
| `SUBTITLEEXTRACTSLATOR_MCP_LOG` | `true` | Enable/disable MCP runtime logs. |
| `SUBTITLEEXTRACTSLATOR_TRANSLATION_PARALLELISM` | `4` | Translation/OCR parallelism (`1..32`). |
| `SUBTITLEEXTRACTSLATOR_CUES_PER_GROUP` | `5` | Default cue count per subtitle group (`1..500`). |
| `SUBTITLEEXTRACTSLATOR_TRANSLATION_BODY_SIZE` | `20` | Default number of groups per translation unit (`1..32`). |
| `SUBTITLEEXTRACTSLATOR_TEMPDIR` | OS temp + `SubtitleExtractslator` | Overrides temp root for snapshots/intermediate files. |
| `SUBTITLEEXTRACTSLATOR_DUMP_PROMPT` | off | Enables extra prompt dump behavior for diagnostics/testing paths. |

## 3.2 LLM provider configuration

| Variable | Default | Purpose |
| --- | --- | --- |
| `LLM_ENDPOINT` | `http://localhost:1234/api/v1/chat` (translation provider) | Translation endpoint. |
| `LLM_MODEL` | `qwen3.5-9b-uncensored-hauhaucs-aggressive` | Target model name hint. |
| `LLM_API_TYPE` | `openai` | Payload mode: `openai` or `claude`. |
| `LLM_AUTH_TYPE` | auto | Auth strategy: `none`, `key`, `azure-rbac`. |
| `LLM_API_KEY` | none | Generic API key source. |
| `OPENAI_API_KEY` | none | Fallback API key source for OpenAI-like mode. |
| `ANTHROPIC_API_KEY` | none | Fallback API key source for Claude mode. |
| `LLM_AZURE_SCOPE` | `https://cognitiveservices.azure.com/.default` | Scope for Azure RBAC token acquisition. |
| `LLM_ANTHROPIC_VERSION` | `2023-06-01` | Anthropic API version header. |
| `LLM_SYSTEM_PROMPT` | built-in default | Optional system prompt override. |
| `LLM_MAX_TOKENS` | `2048` | Max token request field for supported providers. |
| `LLM_REASONING` | unset | Reasoning mode (`off/low/medium/high/on`) where supported. |
| `LLM_RETRY_COUNT` | `3` | Translation retry count (`1..20`) if not overridden by command arg. |

## 3.3 Bitmap OCR branch configuration

| Variable | Default | Purpose |
| --- | --- | --- |
| `SUBTITLEEXTRACTSLATOR_PGS_OCR_ENDPOINT` | fallback to `LLM_ENDPOINT`, then `http://localhost:1234/v1/chat/completions` | OCR endpoint for CLI bitmap branch. |
| `SUBTITLEEXTRACTSLATOR_PGS_OCR_MODEL` | fallback to `LLM_MODEL` | OCR model override. |
| `SUBTITLEEXTRACTSLATOR_PGS_OCR_TIMEOUT_SECONDS` | `120` | OCR request timeout (`5..600`). |
| `SUBTITLEEXTRACTSLATOR_PGS_OCR_MAX_CUES` | `160` | OCR cue cap (`1..2000`) for large bitmap subtitles. |

Notes:

1. OCR ignores global `LLM_REASONING` and applies internal fallback (`off -> low -> unset`).
2. In MCP mode, bitmap OCR uses MCP sampling instead of HTTP endpoint calls.

## 3.4 OpenSubtitles and FFmpeg

| Variable | Default | Purpose |
| --- | --- | --- |
| `OPENSUBTITLES_MOCK` | unset | Enables mock search candidate behavior for offline testing. |
| `FFMPEG_BIN_DIR` | auto-detect | Explicit FFmpeg/ffprobe binary directory override. |

Runtime detection notes:

1. On Windows, known FFmpeg lookup includes WinGet links under user profile directories.
2. On Linux/macOS, known FFmpeg lookup uses common system bin directories.

## 4. Runtime paths and persisted data

Important paths:

1. OpenSubtitles auth cache:
   - Windows: `%LOCALAPPDATA%/SubtitleExtractslator/opensubtitles.auth.json`
   - Linux: `$XDG_DATA_HOME/SubtitleExtractslator/opensubtitles.auth.json` (or `~/.local/share/SubtitleExtractslator/opensubtitles.auth.json`)
   - macOS: `~/Library/Application Support/SubtitleExtractslator/opensubtitles.auth.json`
2. Temp root:
   - `Path.GetTempPath()/SubtitleExtractslator` (or `SUBTITLEEXTRACTSLATOR_TEMPDIR`)
3. Snapshot logs:
   - `<temp-root>/snapshots`
4. Translation history markdown dumps:
   - `<temp-root>/translatehistory`
5. Bitmap extraction artifacts:
   - `<temp-root>/pgs`

## 5. Test and verification strategy

Current test entry point:

- `SubtitleExtractslator.Tests/TranslationBatchTests.cs`

What it verifies:

1. input subtitle discovery from test folders
2. grouped translation flow on each sample file
3. structure invariants (index/time stability)
4. display-width constraints
5. output file generation under `artifacts/test-output/translation-batch`

Optional real-LLM mode:

- Set `SUBTITLEEXTRACTSLATOR_TEST_USE_REAL_LLM=1`

## 6. Common maintenance tasks

1. Update command surface:
   - keep `AppOptions.HelpText`, CLI handlers, and skill reference docs aligned.
2. Update MCP tools:
   - keep `McpTools` descriptions and return contract stable.
3. Tune translation reliability:
   - adjust retry, health-guard, and parsing logic together.
4. Tune OpenSubtitles behavior:
   - preserve auth error guidance and fallback ordering guarantees.
5. Package updates:
   - republish binaries and verify expected files under `subtitle-extractslator/assets/bin/`.

## 7. Related documents

1. [System Architecture](./system-architecture.md)
2. [Implementation Map](./implementation-map.md)
3. [OpenSubtitles Auth and Interface Design](./opensubtitles-auth-and-interface-design.md)
4. [Skill Runtime Maintainer Notes](./skill-runtime-maintainer-notes.md)
5. [Skill Installation and Build](./skill-installation-and-build.md)
