# SubtitleExtractslator Beta NativeAOT Packages

The prerelease runtime set is published from `development`. Install the skill source from that branch; Beta releases do not publish skill ZIPs.

## Restore the Host Runtime

The skill checks the latest prerelease version on every run and downloads only the host RID package when its versioned cache is missing:

```bash
python3 assets/bootstrap/restore_runtime.py --channel beta
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

All RID package IDs in a Beta release set share one prerelease version. `linux-arm` (32-bit) is not included because it has not passed the NativeAOT support gate.

## Install the Skill

```bash
git clone --branch development https://github.com/waynebaby/SubtitleExtractslator.git
npx skills add ./SubtitleExtractslator/.github/skills/subtitle-extractslator
```

## SO-Enhanced Skill Contract

1. Execution basis: `.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json`
2. Compile input only: `.github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md`
3. Audit artifacts: `.github/skills/subtitle-extractslator/assets/so-workflow/audit/`

## GitHub Fallback

Use the moving Beta fallback release only when NuGet is unavailable. It contains exact-version RID `.nupkg` files and matching `.nupkg.sha512` sidecars:

<https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-beta-latest>
