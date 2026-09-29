# SubtitleExtractslator Stable NativeAOT Packages

The stable runtime release set is published from `main`. The skill itself is installed from the repository; stable releases do not publish a skill ZIP.

## Restore the Host Runtime

The skill checks this channel's latest NuGet version on every run and downloads only the host RID package when its versioned cache is missing:

```bash
python3 assets/bootstrap/restore_runtime.py --channel stable
```

On Windows, use `py -3` if `python3` is not available. The bootstrap requires Python 3 and HTTPS access to NuGet. It verifies the package's SHA-512 sidecar and prints the absolute executable path.

## Runtime Packages

Each package contains one NativeAOT executable under `tools/<rid>/`:

- `SubtitleExtractslator.Cli.win-x64`
- `SubtitleExtractslator.Cli.win-arm64`
- `SubtitleExtractslator.Cli.linux-x64`
- `SubtitleExtractslator.Cli.linux-arm64`
- `SubtitleExtractslator.Cli.linux-musl-x64`
- `SubtitleExtractslator.Cli.linux-musl-arm64`
- `SubtitleExtractslator.Cli.osx-x64`
- `SubtitleExtractslator.Cli.osx-arm64`

All RID package IDs in a stable release set share one version. `linux-arm` (32-bit) is not included because it has not passed the NativeAOT support gate.

## Install the Skill

Install the skill from the repository rather than a release archive:

```bash
npx skills add waynebaby/SubtitleExtractslator --skill subtitle-extractslator
```

## SO Execution

- Official run: `dotnet so.dll run --workflow-file <skill-path>/assets/so-workflow/so-template.json`
- Official resume: `dotnet so.dll resume --workflow-file <runtime-workflow-copy>.json --result-file <external-result>.json`
- Direct CLI and MCP are runtime primitives only, not official skill execution history.

## GitHub Fallback

Use the moving stable fallback release only when NuGet is unavailable. It contains the exact-version RID `.nupkg` files and matching `.nupkg.sha512` sidecars:

<https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-stable-latest>
