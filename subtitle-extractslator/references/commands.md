# Command Reference

## Binary Matrix

Single-file outputs are published per RID under `assets/bin/`:

1. `win-x64/SubtitleExtractslator.Cli.exe`
1. `linux-x64/SubtitleExtractslator.Cli`
1. `linux-arm64/SubtitleExtractslator.Cli`
1. `osx-x64/SubtitleExtractslator.Cli`
1. `osx-arm64/SubtitleExtractslator.Cli`

## CLI Commands

### Probe subtitles

Windows example:

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli probe --input "movie.mkv" --lang zh`

Linux/macOS example:

`./assets/bin/linux-x64/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh`

Expected result:

- JSON including target language check and discovered subtitle tracks.

### OpenSubtitles search

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli opensubtitles-search --input "movie.mkv" --lang zh`

Expected result:

- JSON candidate list.
- In current implementation, candidates are mock-driven when `OPENSUBTITLES_MOCK` is set.

### Extract subtitle

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en`

Expected behavior:

- If input is SRT, file is copied.
- Otherwise ffmpeg extraction is attempted from selected subtitle track.

### Full workflow

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt"`

Expected behavior:

1. Probe
1. OpenSubtitles search
1. Fallback extraction
1. Grouping
1. Rolling summary
1. Translation
1. Merge and output

## MCP Mode

Start server:

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode mcp`

Exposed tools:

1. `probe`
1. `opensubtitles_search`
1. `extract`
1. `run_workflow`

## Environment Variables

1. `OPENSUBTITLES_MOCK`

- Any non-empty value enables mock candidate results.

1. LLM translation settings

- Default behavior (when no settings provided):
  - Endpoint: `http://localhost:1234/api/v1/chat`
  - Model: `gemma-3-27b-it`
  - Request format: `{ model, system_prompt, input }`
- Optional overrides:
  - `LLM_ENDPOINT`
  - `LLM_MODEL`
  - `LLM_SYSTEM_PROMPT`
  - `LLM_API_TYPE` (`openai` or `claude`)
  - `LLM_AUTH_TYPE` (`none`, `key`, `azure-rbac`)
  - `LLM_API_KEY` (or `OPENAI_API_KEY` / `ANTHROPIC_API_KEY`)
  - `LLM_AZURE_SCOPE` (default: `https://cognitiveservices.azure.com/.default`)
  - `LLM_ANTHROPIC_VERSION` (default: `2023-06-01`)
