# Binary Missing Recovery

Use this reference when runtime package restore fails or portable DLL runtime is unavailable. The `subtitle-extractslator/` skill package is binary-free and should not be repaired by copying binaries into the skill folder.

This file validates the direct `SubtitleExtractslator.Cli.dll` primitive runtime only. For SO-governed guide refresh and official workflow maintenance, use `dotnet so.dll --guide [--lang <language>]` from the selected SO runtime instead.

## Official Source

Use package channels first. Use the fallback `.nupkg` listed in the chosen package index page only when package feed is unavailable.

<!-- release-links:start -->
- Project URL: [waynebaby/SubtitleExtractslator](https://github.com/waynebaby/SubtitleExtractslator)
- Stable index: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- Beta index: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Runtime fallback .nupkg links are maintained inside the package index pages above.
<!-- release-links:end -->

## Verify Runtime Integrity

1. Confirm `SubtitleExtractslator.Cli` was restored or downloaded from the selected package index page.
1. Confirm the external runtime package contains `lib/net9.0/SubtitleExtractslator.Cli.dll`.
1. Run the component runtime guide command: `dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide`.
1. If guide output succeeds, runtime is considered healthy.

## If `dotnet` Is Missing

1. `SubtitleExtractslator.Cli.dll` targets `net9.0`, so the smallest valid host is the `.NET 9 Runtime` for the current platform and architecture.
1. Install the runtime only. Do not install the full SDK unless you also need `dotnet add package`, `dotnet build`, or `dotnet publish`.
1. Prefer the smallest execution-only path first:
1. runtime installer for `.NET 9 Runtime`
1. extracted portable runtime already approved by your environment
1. After runtime install, re-run `dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide` before any probe/extract/translate command.

## Recovery Flow

1. Retry package restore from stable/beta index instructions.
1. If feed is unavailable, use the current fallback `.nupkg` link listed in the selected package index page.
1. If `dotnet` is missing, install the minimal `.NET 9 Runtime` first; avoid the SDK for execution-only recovery.
1. Restore or extract the package outside the skill folder.
1. Re-run the component runtime guide command: `dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide`.
1. Re-run a minimal command (`probe` or `translate`) to validate runtime availability.
1. If FFmpeg is still missing, follow `references/localpaths.md` and set `FFMPEG_BIN_DIR`.


