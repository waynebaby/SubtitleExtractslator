# Binary Missing Recovery

Use this reference when NativeAOT runtime restore fails or the platform executable is unavailable. The skill source is binary-free; do not copy runtime binaries into the skill folder.

For SO-governed guide refresh and workflow maintenance, run the extracted SO apphost directly: `so.exe --guide` on Windows or `so --guide` on Unix. The subtitle CLI itself is a RID-specific NativeAOT executable and does not require the .NET runtime.

## Official Source

Use package channels first. Use the fallback `.nupkg` listed in the chosen package index page only when package feed is unavailable.

<!-- release-links:start -->
- Project URL: [waynebaby/SubtitleExtractslator](https://github.com/waynebaby/SubtitleExtractslator)
- Stable index: [packages.released.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md)
- Beta index: [packages.beta.md](https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md)
- Runtime fallback .nupkg links are maintained inside the package index pages above.
<!-- release-links:end -->

## Verify Runtime Integrity

1. Run `assets/bootstrap/restore_runtime.py --channel <metadata.channel>` using Python 3.
1. Confirm the resolved package ID matches the detected host RID, for example `SubtitleExtractslator.Cli.win-arm64`.
1. Confirm the package contains `tools/<rid>/SubtitleExtractslator.Cli[.exe]` and its SHA-512 sidecar verified.
1. Run the resolved native executable with `--guide`.
1. If guide output succeeds, runtime is considered healthy.

## If Python or Network Access Is Missing

1. The bootstrap requires Python 3 and outbound HTTPS access to `api.nuget.org` to check the channel version.
1. If Python is unavailable, install Python 3 or manually download the exact host-RID `.nupkg` and matching `.sha512` asset from the selected fallback release.
1. Verify the SHA-512 sidecar before extracting `tools/<rid>/` outside the skill folder.
1. Do not install the .NET runtime or SDK to execute the NativeAOT CLI.

## Recovery Flow

1. Confirm the skill's `metadata.channel` is `stable` or `beta` and the host maps to a supported RID.
1. Retry `assets/bootstrap/restore_runtime.py --channel <metadata.channel>`.
1. If NuGet is unavailable, use the exact RID/version `.nupkg` and `.sha512` fallback assets listed for the matching channel.
1. Verify the sidecar, then extract the package's `tools/<rid>/` payload outside the skill folder.
1. Run the native executable with `--guide`, then retry a minimal `probe` or `subtitle auth status` command.
1. If FFmpeg is still missing, follow `references/localpaths.md` and set `FFMPEG_BIN_DIR`.



