# Command Reference

## CLI Commands

### Probe subtitles

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli probe --input "movie.mkv" --lang zh`

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
2. OpenSubtitles search
3. Fallback extraction
4. Grouping
5. Rolling summary
6. Translation
7. Merge and output

## MCP Mode

Start server:

`./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode mcp`

Exposed tools:
1. `probe`
2. `opensubtitles_search`
3. `extract`
4. `run_workflow`

## Environment Variables

1. `OPENSUBTITLES_MOCK`
- Any non-empty value enables mock candidate results.

2. `TRANSLATION_MODE`
- `noop`: preserve original subtitle text.
- `prefix`: prefix translated text with target language tag for testing.
