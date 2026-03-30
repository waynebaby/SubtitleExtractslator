# Binary Missing Recovery

Use this reference when the installed skill package does not contain runtime binaries under `assets/bin/<rid>/`.

## Official Source

Always download from GitHub release artifacts for deterministic package completeness.

<!-- release-links:start -->
- Project URL: [waynebaby/SubtitleExtractslator](https://github.com/waynebaby/SubtitleExtractslator)
- Releases URL: [Releases](https://github.com/waynebaby/SubtitleExtractslator/releases)
- Windows x64 package (v0.1.12): [subtitle-extractslator-v0.1.12-win-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-win-x64.zip)
- Windows ARM64 package (v0.1.12): [subtitle-extractslator-v0.1.12-win-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-win-arm64.zip)
- Linux x64 package (v0.1.12): [subtitle-extractslator-v0.1.12-linux-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-linux-x64.zip)
- Linux musl x64 package (v0.1.12): [subtitle-extractslator-v0.1.12-linux-musl-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-linux-musl-x64.zip)
- Linux ARM64 package (v0.1.12): [subtitle-extractslator-v0.1.12-linux-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-linux-arm64.zip)
- Linux musl ARM64 package (v0.1.12): [subtitle-extractslator-v0.1.12-linux-musl-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-linux-musl-arm64.zip)
- Linux ARM package (v0.1.12): [subtitle-extractslator-v0.1.12-linux-arm.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-linux-arm.zip)
- macOS ARM64 package (v0.1.12): [subtitle-extractslator-v0.1.12-osx-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-osx-arm64.zip)
- macOS x64 package (v0.1.12): [subtitle-extractslator-v0.1.12-osx-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.12/subtitle-extractslator-v0.1.12-osx-x64.zip)
<!-- release-links:end -->

## Verify Package Integrity

1. Unzip package root and confirm `subtitle-extractslator/SKILL.md` exists.
2. Confirm your runtime RID directory exists:
- Windows x64: `subtitle-extractslator/assets/bin/win-x64/`
- Windows ARM64: `subtitle-extractslator/assets/bin/win-arm64/`
- Linux x64: `subtitle-extractslator/assets/bin/linux-x64/`
- Linux musl x64: `subtitle-extractslator/assets/bin/linux-musl-x64/`
- Linux ARM64: `subtitle-extractslator/assets/bin/linux-arm64/`
- Linux musl ARM64: `subtitle-extractslator/assets/bin/linux-musl-arm64/`
- Linux ARM: `subtitle-extractslator/assets/bin/linux-arm/`
- macOS ARM64: `subtitle-extractslator/assets/bin/osx-arm64/`
- macOS x64: `subtitle-extractslator/assets/bin/osx-x64/`
3. Confirm executable file exists inside your RID directory:
- Windows: `SubtitleExtractslator.Cli.exe`
- Linux/macOS: `SubtitleExtractslator.Cli`

## Recovery Flow

1. Delete incomplete local skill copy.
2. Download the correct platform zip from release links above.
3. Replace local skill folder with the extracted `subtitle-extractslator` folder.
4. Re-run a minimal command (`probe` or `translate`) to validate runtime availability.
5. If FFmpeg is still missing, follow `references/localpaths.md` and set `FFMPEG_BIN_DIR`.


