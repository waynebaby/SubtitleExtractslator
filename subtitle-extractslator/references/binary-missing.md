# Binary Missing Recovery

Use this reference when runtime package restore fails or portable DLL runtime is unavailable.

## Official Source

Use package channels first. Use GitHub fallback `.nupkg` only when package feed is unavailable.

<!-- release-links:start -->
- Project URL: [waynebaby/SubtitleExtractslator](https://github.com/waynebaby/SubtitleExtractslator)
- Stable index: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- Beta index: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Stable fallback release: [nuget-stable-latest](https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-stable-latest)
- Stable latest package: [SubtitleExtractslator.Cli.latest.nupkg](https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-stable-latest/SubtitleExtractslator.Cli.latest.nupkg)
- Beta fallback release: [nuget-beta-latest](https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-beta-latest)
- Beta latest package: [SubtitleExtractslator.Cli.latest.nupkg](https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-beta-latest/SubtitleExtractslator.Cli.latest.nupkg)
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




