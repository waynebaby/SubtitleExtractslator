# CLI Reference

## Runtime Paths

Execution path rules:
1. Paths are deterministic and relative to skill root.
2. Do not scan the whole disk to locate binaries.
3. If current directory is repository root, prepend `./.github/skills/subtitle-extractslator/`.
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
1. Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --help`
2. Windows ARM64: `./assets/bin/win-arm64/SubtitleExtractslator.Cli.exe --help`
3. Linux x64: `./assets/bin/linux-x64/SubtitleExtractslator.Cli --help`
4. Linux musl x64: `./assets/bin/linux-musl-x64/SubtitleExtractslator.Cli --help`
5. Linux ARM64: `./assets/bin/linux-arm64/SubtitleExtractslator.Cli --help`
6. Linux musl ARM64: `./assets/bin/linux-musl-arm64/SubtitleExtractslator.Cli --help`
7. Linux ARM: `./assets/bin/linux-arm/SubtitleExtractslator.Cli --help`
8. macOS ARM64: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli --help`
9. macOS x64: `./assets/bin/osx-x64/SubtitleExtractslator.Cli --help`

## Global Options

1. `--env "KEY=VALUE;KEY2=VALUE2"`: temporary environment overrides for current command.
2. `--help`: print complete CLI command help.

## Output Path Policy (Critical)

1. Final subtitle output must be in the same folder as input video (or input subtitle) unless user explicitly requests another folder.
2. For `run-workflow`, always pass explicit `--output` in input folder.
3. If output name is not provided, use `<input_basename>.<lang>.srt` in input folder.
4. Do not place final outputs in random/temp directories. Temp files are allowed only as intermediate artifacts.

## CLI Commands

1. Probe:
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli probe --input "movie.mkv" --lang zh`
2. OpenSubtitles search:
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli opensubtitles-search --input "movie.mkv" --lang zh`
3. Extract:
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli extract --input "movie.mkv" --out "movie.en.srt" --prefer en`
4. Full workflow:
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli run-workflow --input "movie.mkv" --lang zh --output "movie.zh.srt"`
5. Batch workflow (CLI only):
- Windows: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe --mode cli run-workflow-batch --input-list ".\\inputs.txt" --lang zh --output-dir ".\\out" --output-suffix ".zh.srt"`

## Batch Input Rules

1. UTF-8 text file.
2. One input path per line.
3. Empty lines and lines starting with `#` are ignored.
4. Output suffix defaults to `.<lang>.srt` when `--output-suffix` is omitted.
