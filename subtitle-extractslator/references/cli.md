# CLI Reference

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
2. OpenSubtitles search:
- `<cli_bin> --mode cli opensubtitles-search --input "movie.mkv" --lang zh --search-query-primary "movie" --search-query-normalized "movie s00e00" --opensubtitles-api-key "<key>" [--opensubtitles-username "<user>"] [--opensubtitles-password "<pass>"] [--opensubtitles-endpoint "<url>"] [--opensubtitles-user-agent "<ua>"]`
3. OpenSubtitles download:
- By ranked search candidate (default rank 1): `<cli_bin> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt" --opensubtitles-api-key "<key>"`
- By rank override: `<cli_bin> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt" --opensubtitles-api-key "<key>" --candidate-rank 2`
- By direct file id: `<cli_bin> --mode cli opensubtitles-download --input "movie.mkv" --lang zh --output "movie.zh.opensub.srt" --opensubtitles-api-key "<key>" --file-id 1234567`
4. Download notes:
- Ranked download reuses mandatory OpenSubtitles fallback search strategy (base filename first, then `<series_or_title> s00e00` from normalized full path).
- Direct file-id download skips search.
- Real API access requires explicit `--opensubtitles-api-key` parameter and sends `Api-Key` and `User-Agent` headers.
5. Extract:
- `<cli_bin> --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en`
6. Bitmap subtitle branch (PGS/DVD):
- If selected subtitle codec is bitmap (`hdmv_pgs_subtitle` or `dvd_subtitle`), extract now runs: SUP export -> built-in SUP decode to PNG + timeline -> OCR -> SRT.
- The render-overlay screenshot path is disabled; PNG frames must come from SUP conversion.
- Intermediate artifacts are written under temp root `Path.GetTempPath()/SubtitleExtractslator/pgs` (or `SUBTITLEEXTRACTSLATOR_TEMPDIR` override).
- SUP-to-PNG decoding is built in (pure C# parser + ImageSharp). No external converter command is required.
- OCR is built in C# and calls a local OpenAI-compatible chat completion endpoint directly (no script dependency).
- OCR endpoint env: `LLM_ENDPOINT` (default `http://localhost:1234/v1/chat/completions`).
- OCR model env: `LLM_MODEL` (default `qwen3.5-9b-uncensored-hauhaucs-aggressive`).
- OCR model/endpoint requirement: must support multimodal image input (`image_url` in OpenAI-compatible chat completions payload).
- OCR timeout env: `SUBTITLEEXTRACTSLATOR_PGS_OCR_TIMEOUT_SECONDS` (default `120`, clamp `5..600`).
- OCR ignores `LLM_REASONING` and always uses fixed fallback order: `off` -> `low` -> unset.
- Reasoning fallback order for OCR requests: `off` -> `low` -> unset.
- `SUBTITLEEXTRACTSLATOR_PGS_OCR_MAX_CUES` controls OCR cue cap (default `160`, clamp `1..2000`).
7. Translate (CLI only):
- `<cli_bin> --mode cli translate --input "movie.en.srt" --lang zh --output "movie.zh.srt" [--cues-per-group <n>] [--body-size <n>] [--llm-retry-count <n>]`
8. Translate batch (CLI only):
- `<cli_bin> --mode cli translate-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"`

## Batch Input Rules

1. UTF-8 text file.
2. One input path per line.
3. Empty lines and lines starting with `#` are ignored.
4. Output suffix defaults to `.<lang>.srt` when `--output-suffix` is omitted.
