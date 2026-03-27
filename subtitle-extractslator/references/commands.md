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

Platform invocation rule:

1. Pick the matching binary from Binary Matrix and refer to it as `<cli_bin>`.
2. Use the same command shape across platforms by replacing only `<cli_bin>`.

### Probe subtitles

Template:

`<cli_bin> --mode cli probe --input "movie.mkv" --lang zh`

Expected result:

- JSON including target language check and discovered subtitle tracks.

### OpenSubtitles search

`<cli_bin> --mode cli opensubtitles-search --input "movie.mkv" --lang zh --opensubtitles-api-key "<key>"`

Expected result:

- JSON candidate list.
- Real API search is used when `--opensubtitles-api-key` is provided.
- Mock candidates are returned only when `OPENSUBTITLES_MOCK` is set.
- Optional OpenSubtitles parameters: `--opensubtitles-username`, `--opensubtitles-password`, `--opensubtitles-endpoint`, `--opensubtitles-user-agent`.

### OpenSubtitles download

`<cli_bin> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt" --opensubtitles-api-key "<key>"`

Additional options:

- `--candidate-rank <n>`: download by ranked search result (default `1`)
- `--file-id <id>`: direct download by OpenSubtitles file id (skips search)

Expected behavior:

- Ranked download uses mandatory fallback search strategy: base filename first, then normalized full-path query like `<series_or_title> s00e00`.
- Real download requires `--opensubtitles-api-key`.
- HTTP integration sends `Api-Key` and `User-Agent` headers.

### Extract subtitle

`<cli_bin> --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en`

Expected behavior:

- If input is SRT, file is copied.
- Otherwise ffmpeg extraction is attempted from selected subtitle track.

### Full workflow

`<cli_bin> --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt" [--opensubtitles-api-key "<key>"] [--opensubtitles-username "<user>"] [--opensubtitles-password "<pass>"] [--opensubtitles-endpoint "<url>"] [--opensubtitles-user-agent "<ua>"]`

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

`<cli_bin> --mode cli run-workflow-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"`

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

`<cli_bin> --mode mcp`

Exposed tools:

1. `probe`
1. `opensubtitles_search`
1. `opensubtitles_download`
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
2. MCP `opensubtitles_download` is download-only and must use `fileId` from a prior `opensubtitles_search` result.

## Environment Variables

1. `OPENSUBTITLES_MOCK`

- Any non-empty value enables mock candidate results.

1. OpenSubtitles credential rule

- OpenSubtitles credentials are explicit command/tool parameters, not environment variables.
- Required: `opensubtitlesApiKey` (CLI: `--opensubtitles-api-key`).
- Optional: `opensubtitlesUsername`, `opensubtitlesPassword`, `opensubtitlesEndpoint`, `opensubtitlesUserAgent`.

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
