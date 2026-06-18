# Binary Missing Recovery

Use this reference when runtime package restore fails or portable DLL runtime is unavailable.

## Official Source

Use package channels first. Use GitHub fallback `.nupkg` only when package feed is unavailable.

<!-- release-links:start -->
- Project URL: [waynebaby/SubtitleExtractslator](https://github.com/waynebaby/SubtitleExtractslator)
- Releases URL: [Releases](https://github.com/waynebaby/SubtitleExtractslator/releases)
- Windows x64 package (v0.1.17): [subtitle-extractslator-v0.1.17-win-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-win-x64.zip)
- Windows ARM64 package (v0.1.17): [subtitle-extractslator-v0.1.17-win-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-win-arm64.zip)
- Linux x64 package (v0.1.17): [subtitle-extractslator-v0.1.17-linux-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-linux-x64.zip)
- Linux musl x64 package (v0.1.17): [subtitle-extractslator-v0.1.17-linux-musl-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-linux-musl-x64.zip)
- Linux ARM64 package (v0.1.17): [subtitle-extractslator-v0.1.17-linux-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-linux-arm64.zip)
- Linux musl ARM64 package (v0.1.17): [subtitle-extractslator-v0.1.17-linux-musl-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-linux-musl-arm64.zip)
- Linux ARM package (v0.1.17): [subtitle-extractslator-v0.1.17-linux-arm.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-linux-arm.zip)
- macOS ARM64 package (v0.1.17): [subtitle-extractslator-v0.1.17-osx-arm64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-osx-arm64.zip)
- macOS x64 package (v0.1.17): [subtitle-extractslator-v0.1.17-osx-x64.zip](https://github.com/waynebaby/SubtitleExtractslator/releases/download/v0.1.17/subtitle-extractslator-v0.1.17-osx-x64.zip)
<!-- release-links:end -->

## Verify Runtime Integrity

1. Confirm package restore/build succeeds for `SubtitleExtractslator.Cli`.
2. Confirm runtime output contains `SubtitleExtractslator.Cli.dll`.
3. Run guide-first command:
	- `dotnet SubtitleExtractslator.Cli.dll --guide`
4. If guide output succeeds, runtime is considered healthy.

## Recovery Flow

1. Retry package restore from stable/beta index instructions.
2. If feed is unavailable, download fallback `.nupkg` from links above.
3. Add/use package from local `.nupkg` and rebuild runtime output.
4. Re-run `dotnet SubtitleExtractslator.Cli.dll --guide`.
5. Re-run a minimal command (`probe` or `translate`) to validate runtime availability.
6. If FFmpeg is still missing, follow `references/localpaths.md` and set `FFMPEG_BIN_DIR`.







