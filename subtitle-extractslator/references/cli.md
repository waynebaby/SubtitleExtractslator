# CLI Reference

This file is skill-facing runtime contract only.


## Runtime Paths

Execution path rules:
1. Paths are deterministic and relative to skill root.
2. Do not scan the whole disk to locate binaries.
3. If current directory is repository root, prepend your agent workspace skill path: `./.github/skills/subtitle-extractslator/` or `./.claude/skills/subtitle-extractslator/`.
4. Quote paths with spaces.

Platform binaries:
1. Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe`
2. Windows ARM64: `./assets/bin/win-arm64/SubtitleExtractslator.Cli.exe`
3. Linux x64: `./assets/bin/linux-x64/SubtitleExtractslator.Cli`
4. Linux musl x64 (Alpine): `./assets/bin/linux-musl-x64/SubtitleExtractslator.Cli`
5. Linux ARM64: `./assets/bin/linux-arm64/SubtitleExtractslator.Cli`
6. Linux musl ARM64 (Alpine): `./assets/bin/linux-musl-arm64/SubtitleExtractslator.Cli`
7. Linux ARM (32-bit): `./assets/bin/linux-arm/SubtitleExtractslator.Cli`
8. macOS ARM64 (Apple Silicon): `./assets/bin/osx-arm64/SubtitleExtractslator.Cli`
9. macOS x64 (Intel): `./assets/bin/osx-x64/SubtitleExtractslator.Cli`

Quick check:
1. Pick the matching binary from Platform binaries and refer to it as `<cli_bin>`.
2. Run: `<cli_bin> --help`

## Global Options

1. `--env "KEY=VALUE;KEY2=VALUE2"`: temporary environment overrides for current command.
2. `--help`: print complete CLI command help.

## Output Path Notes

1. `translate` requires explicit `--output`.
2. `translate-batch` requires explicit `--output-dir`.
3. `translate-batch` defaults `--output-suffix` to `.<lang>.srt` when omitted.
4. If you need deterministic paths, set explicit output/output-dir values in commands.

## CLI Commands

Platform command rule:
1. Replace all examples below with your selected `<cli_bin>`.

1. Probe:
- `<cli_bin> --mode cli probe --input "movie.mkv" --lang zh`
2. Subtitle timing check:
- `<cli_bin> --mode cli subtitle-timing-check --input "movie.mkv" --subtitle "movie.zh.srt"`
- Returns whether `abs(video_duration - subtitle_last_cue_end) < 600 seconds`.
3. OpenSubtitles search:
- `<cli_bin> --mode cli opensubtitles-search --input "movie.mkv" --lang zh --search-query-primary "movie" --search-query-normalized "movie s00e00" [--opensubtitles-endpoint "<url>"] [--opensubtitles-user-agent "<ua>"]`
4. OpenSubtitles download:
- By ranked search candidate (default rank 1): `<cli_bin> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt"`
- By rank override: `<cli_bin> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt" --candidate-rank 2`
- By direct file id: `<cli_bin> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt" --file-id 1234567`
5. Download notes:
- Ranked download reuses mandatory OpenSubtitles fallback search strategy (base filename first, then `<series_or_title> s00e00` from normalized full path).
- Direct file-id download skips search.
- Real API access uses `subtitle auth login` cache and sends `Api-Key` and `User-Agent` headers.
6. Auth commands:
- `subtitle auth login --api-key "<key>" --username "<user>" --password "<pass>" [--opensubtitles-endpoint "<url>"] [--opensubtitles-user-agent "<ua>"]`
- `subtitle auth aquire`
- `subtitle auth status`
- `subtitle auth clear`
7. Auth notes:
- `login` is the only write operation.
- `clear` is the only delete operation.
- Password prompt hides keystrokes when interactive input is used.
8. Extract:
- `<cli_bin> --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en`
9. Translate (CLI only):
- `<cli_bin> --mode cli translate --input "movie.en.srt" --lang zh --output "movie.zh.srt" [--cues-per-group <n>] [--body-size <n>] [--llm-retry-count <n>]`
10. Translate batch (CLI only):
- `<cli_bin> --mode cli translate-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"`

Detailed bitmap OCR runtime internals and environment defaults are documented in `docs/skill-runtime-maintainer-notes.md`.

## Batch Input Rules

1. UTF-8 text file.
2. One input path per line.
3. Empty lines and lines starting with `#` are ignored.
4. Output suffix defaults to `.<lang>.srt` when `--output-suffix` is omitted.
