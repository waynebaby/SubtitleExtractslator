# Command Reference

## Binary Matrix

Single-file outputs are published per RID under `assets/bin/`:

1. `win-x64/SubtitleExtractslator.Cli.exe`
1. `win-arm64/SubtitleExtractslator.Cli.exe`
1. `linux-x64/SubtitleExtractslator.Cli`
1. `linux-musl-x64/SubtitleExtractslator.Cli`
1. `linux-arm64/SubtitleExtractslator.Cli`
1. `linux-musl-arm64/SubtitleExtractslator.Cli`
1. `linux-arm/SubtitleExtractslator.Cli`
1. `osx-x64/SubtitleExtractslator.Cli`
1. `osx-arm64/SubtitleExtractslator.Cli`

## CLI Commands

Global CLI options:

1. `--env "KEY=VALUE;KEY2=VALUE2"` applies per-command temporary environment overrides.
1. `--help` prints complete CLI help text.

### Probe subtitles

Windows example:

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli probe --input "movie.mkv" --lang zh`

Linux x64 example:

`./assets/bin/linux-x64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`

Linux musl x64 example:

`./assets/bin/linux-musl-x64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`

Linux ARM64 example:

`./assets/bin/linux-arm64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`

Linux musl ARM64 example:

`./assets/bin/linux-musl-arm64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`

Linux ARM (32-bit) example:

`./assets/bin/linux-arm/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`

macOS ARM64 example:

`./assets/bin/osx-arm64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`

macOS x64 example:

`./assets/bin/osx-x64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`

Expected result:

- JSON including target language check and discovered subtitle tracks.

### OpenSubtitles search

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli opensubtitles-search --input "movie.mkv" --lang zh`

Expected result:

- JSON candidate list.
- Real API search is used when `OPENSUBTITLES_API_KEY` is configured.
- Mock candidates are returned only when `OPENSUBTITLES_MOCK` is set.
- If real API credentials are missing, ask user for credential input before retrying real search.
- Use temporary `--env` overrides for credential answers in CLI runs (do not persist secrets by default).

### Extract subtitle

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en`

Expected behavior:

- If input is SRT, file is copied.
- Otherwise ffmpeg extraction is attempted from selected subtitle track.

### Full workflow

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt"`

Expected behavior:

1. Probe
1. Embedded subtitle extraction (prefer `en/eng`, else another embedded language)
1. If no embedded subtitles: search local folder/subfolders for `*.srt` (prefer `en/eng`)
1. If still none: OpenSubtitles search in any language (prefer `en/eng`)
1. Grouping
1. Rolling summary
1. Translation
1. Merge and output

### Batch workflow (CLI only)

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli run-workflow-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"`

Input list file rules:

1. UTF-8 text file.
1. One path per line.
1. Empty lines and lines starting with `#` are ignored.

Expected behavior:

1. Process each listed input sequentially with `run-workflow`.
1. Generate output paths under `--output-dir` using source file name + `--output-suffix`.
1. Return summary JSON with per-item success/failure details.

## MCP Mode

Start server:

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode mcp`

Other platform start examples:

1. Windows ARM64: `./assets/bin/win-arm64/SubtitleExtractslator.Cli.exe --mode mcp`
1. Linux x64: `./assets/bin/linux-x64/SubtitleExtractslator.Cli --mode mcp`
1. Linux musl x64: `./assets/bin/linux-musl-x64/SubtitleExtractslator.Cli --mode mcp`
1. Linux ARM64: `./assets/bin/linux-arm64/SubtitleExtractslator.Cli --mode mcp`
1. Linux musl ARM64: `./assets/bin/linux-musl-arm64/SubtitleExtractslator.Cli --mode mcp`
1. Linux ARM: `./assets/bin/linux-arm/SubtitleExtractslator.Cli --mode mcp`
1. macOS ARM64: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli --mode mcp`
1. macOS x64: `./assets/bin/osx-x64/SubtitleExtractslator.Cli --mode mcp`

Exposed tools:

1. `probe`
1. `opensubtitles_search`
1. `extract`
1. `run_workflow`

Tool return contract:

1. MCP tools return a structured object:
  - `ok`: boolean
  - `data`: tool payload when successful
  - `error`: error object when failed
2. Error object fields:
  - `code`
  - `message`
  - `snapshotPath` (nullable)
  - `timeUtc`

Notes:

1. Batch workflow is intentionally not exposed in MCP mode to reduce timeout-related failures in MCP clients.

## Environment Variables

1. `OPENSUBTITLES_MOCK`

- Any non-empty value enables mock candidate results.

1. OpenSubtitles real API settings

- `OPENSUBTITLES_API_KEY` (required for real search)
- `OPENSUBTITLES_USERNAME` (optional, used with password for login token)
- `OPENSUBTITLES_PASSWORD` (optional)
- `OPENSUBTITLES_ENDPOINT` (optional, default `https://api.opensubtitles.com/api/v1`)
- `OPENSUBTITLES_USER_AGENT` (optional, default `SubtitleExtractslator/0.1`)

Credential interaction guidance:

- When `OPENSUBTITLES_API_KEY` is absent and user requests OpenSubtitles path, ask for key first.
- Then ask whether to provide username/password for better download reliability.
- Inject user answers as temporary env overrides for the current command/session.

1. LLM translation settings

- Default behavior (when no settings provided):
  - Endpoint: `http://localhost:1234/api/v1/chat`
  - Model: `qwen3.5-9b-uncensored-hauhaucs-aggressive`
  - Request format: `{ model, system_prompt, input }`
- Optional overrides:
  - `LLM_ENDPOINT`
  - `LLM_MODEL`
  - `LLM_SYSTEM_PROMPT`
  - `LLM_API_TYPE` (`openai` or `claude`)
  - `LLM_AUTH_TYPE` (`none`, `key`, `azure-rbac`)
  - `LLM_API_KEY` (or `OPENAI_API_KEY` / `ANTHROPIC_API_KEY`)
  - `LLM_MAX_TOKENS`
  - `LLM_REASONING` (`off`, `low`, `medium`, `high`, `on`)
  - `LLM_RETRY_COUNT`
  - `LLM_AZURE_SCOPE` (default: `https://cognitiveservices.azure.com/.default`)
  - `LLM_ANTHROPIC_VERSION` (default: `2023-06-01`)

1. Runtime logging knobs

- `SUBTITLEEXTRACTSLATOR_CLI_LOG` (`true`/`false`)
- `SUBTITLEEXTRACTSLATOR_MCP_LOG` (`true`/`false`)
- `SUBTITLEEXTRACTSLATOR_TRANSLATION_PARALLELISM` (default `4`)
- `SUBTITLEEXTRACTSLATOR_CUES_PER_GROUP` (default `5`)
- `SUBTITLEEXTRACTSLATOR_TRANSLATION_BODY_SIZE` (default `20`)
