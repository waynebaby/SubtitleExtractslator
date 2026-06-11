# CLI Reference

This file is skill-facing runtime contract only.

## Runtime Package Source

1. The `subtitle-extractslator/` skill package is binary-free. Do not expect `./assets/bin/`.
2. Acquire `SubtitleExtractslator.Cli` from this repository's package index pages:

- stable: `https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md`
- beta: `https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md`

1. If package feed is unavailable, use the current fallback `.nupkg` link listed in the selected package index page.
2. After restore or `.nupkg` extraction, locate `lib/net9.0/SubtitleExtractslator.Cli.dll` outside the skill folder.

## Runtime Entry

Execution path rules:

1. Use an absolute path to the external runtime package DLL.
2. Do not scan the skill folder for binaries.
3. Quote paths with spaces.
4. Refer to `dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll"` as `<cli_entry>`.

Quick check:

1. Resolve `<cli_entry>` from the restored or extracted package.
2. Run: `<cli_entry> --help`

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

1. Replace all examples below with your selected `<cli_entry>`.

1. Probe:

- `<cli_entry> --mode cli probe --input "movie.mkv" --lang zh`

1. Subtitle timing check:

- `<cli_entry> --mode cli subtitle-timing-check --input "movie.mkv" --subtitle "movie.zh.srt"`
- Returns whether `abs(video_duration - subtitle_last_cue_end) < 600 seconds`.

1. OpenSubtitles search:

- `<cli_entry> --mode cli opensubtitles-search --input "movie.mkv" --lang zh --search-query-primary "movie" --search-query-normalized "movie s00e00" [--opensubtitles-endpoint "<url>"] [--opensubtitles-user-agent "<ua>"]`

1. OpenSubtitles download:

- By ranked search candidate (default rank 1): `<cli_entry> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt"`
- By rank override: `<cli_entry> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt" --candidate-rank 2`
- By direct file id: `<cli_entry> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt" --file-id 1234567`

1. Download notes:

- Ranked download reuses mandatory OpenSubtitles fallback search strategy (base filename first, then `<series_or_title> s00e00` from normalized full path).
- Direct file-id download skips search.
- Real API access uses `subtitle auth login` cache and sends `Api-Key` and `User-Agent` headers.

1. Auth commands:

- `<cli_entry> --mode cli subtitle auth login --api-key "<key>" --username "<user>" --password "<pass>" [--opensubtitles-endpoint "<url>"] [--opensubtitles-user-agent "<ua>"]`
- `<cli_entry> --mode cli subtitle auth aquire`
- `<cli_entry> --mode cli subtitle auth status`
- `<cli_entry> --mode cli subtitle auth clear`

1. Auth notes:

- `login` is the only write operation.
- `clear` is the only delete operation.
- Password prompt hides keystrokes when interactive input is used.

1. Extract:

- `<cli_entry> --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en`

1. Translate (CLI only):

- `<cli_entry> --mode cli translate --input "movie.en.srt" --lang zh --output "movie.zh.srt" [--cues-per-group <n>] [--body-size <n>] [--llm-retry-count <n>]`

1. Translate batch (CLI only):

- `<cli_entry> --mode cli translate-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"`

Detailed bitmap OCR runtime internals and environment defaults are documented in `docs/skill-runtime-maintainer-notes.md`.

## Batch Input Rules

1. UTF-8 text file.
2. One input path per line.
3. Empty lines and lines starting with `#` are ignored.
4. Output suffix defaults to `.<lang>.srt` when `--output-suffix` is omitted.

## Long-Running Batch Queue State

When orchestrating folder/season/library runs around CLI commands:

1. Keep queue tracking files in centralized temp storage, not in media target folders.
2. Temp root is `SUBTITLEEXTRACTSLATOR_TEMPDIR` when set, otherwise OS temp root + `SubtitleExtractslator`.
3. State workspace path is `<temp-root>/agent-runs/<run-id>/`.
4. Required tracking files: `queue.txt`, `completed.txt`, `failed.txt`, `in-progress.txt`, `run-notes.md`.
5. Use deterministic output naming for completion checks: `<basename>.<lang>.srt`.
6. If user intent is "finish all" or equivalent, progress updates should not ask for continuation permission.
