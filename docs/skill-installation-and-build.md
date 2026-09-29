# SubtitleExtractslator Skill Build and Installation

This document follows the repository skill layout and runtime packaging contract.

The skill is installed from the GitHub repository. Stable and Beta releases publish only RID-specific NativeAOT NuGet runtime packages; they do not publish skill ZIPs or place binaries inside `.github/skills/subtitle-extractslator/`.

## Skill Folder

Skill root:

- `.github/skills/subtitle-extractslator/` in this repository

Required file:

- `.github/skills/subtitle-extractslator/SKILL.md`

Optional resources used here:

- `.github/skills/subtitle-extractslator/references/commands.md`
- `.github/skills/subtitle-extractslator/references/troubleshooting.md`
- `.github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md`
- `.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json`
- `.github/skills/subtitle-extractslator/assets/so-workflow/audit/`

No README is placed inside the skill folder.

Binary-free rule:

1. Do not ship `.github/skills/subtitle-extractslator/assets/bin/` in the final skill package.
2. Do not publish a skill ZIP. Install the skill source from the repository:
   - Stable: `npx skills add waynebaby/SubtitleExtractslator --skill subtitle-extractslator`
   - Beta: install from the `development` branch of this repository.
3. Before skill work, run `assets/bootstrap/restore_runtime.py --channel <metadata.channel>` with Python 3. It resolves and restores the host RID package from NuGet.
4. If NuGet is unavailable, use the exact RID `.nupkg` and `.sha512` fallback assets from the selected channel release.

The runtime package IDs follow `SubtitleExtractslator.Cli.<rid>` and are published at one shared version per channel release set. Supported RIDs are:

`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, and `osx-arm64`.

`linux-arm` (32-bit) is not included in NativeAOT releases.

Package indexes:

   - `https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md`
   - `https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md`

## Build Locally (Contributor Path)

From repository root, stage the source-only skill folder:

```powershell
New-Item -ItemType Directory -Path ".\tmp\skill\subtitle-extractslator\references" -Force | Out-Null
New-Item -ItemType Directory -Path ".\tmp\skill\subtitle-extractslator\assets\so-workflow" -Force | Out-Null
New-Item -ItemType Directory -Path ".\tmp\skill\subtitle-extractslator\assets\bootstrap" -Force | Out-Null
Copy-Item ".\.github\skills\subtitle-extractslator\SKILL.md" ".\tmp\skill\subtitle-extractslator\SKILL.md" -Force
Copy-Item ".\.github\skills\subtitle-extractslator\references\*" ".\tmp\skill\subtitle-extractslator\references" -Recurse -Force
Copy-Item ".\.github\skills\subtitle-extractslator\assets\so-workflow\*" ".\tmp\skill\subtitle-extractslator\assets\so-workflow" -Recurse -Force
Copy-Item ".\.github\skills\subtitle-extractslator\assets\bootstrap\*" ".\tmp\skill\subtitle-extractslator\assets\bootstrap" -Recurse -Force
```

```bash
mkdir -p ./tmp/skill/subtitle-extractslator/references
mkdir -p ./tmp/skill/subtitle-extractslator/assets/so-workflow
mkdir -p ./tmp/skill/subtitle-extractslator/assets/bootstrap
cp ./.github/skills/subtitle-extractslator/SKILL.md ./tmp/skill/subtitle-extractslator/SKILL.md
cp -R ./.github/skills/subtitle-extractslator/references/* ./tmp/skill/subtitle-extractslator/references/
cp -R ./.github/skills/subtitle-extractslator/assets/so-workflow/* ./tmp/skill/subtitle-extractslator/assets/so-workflow/
cp -R ./.github/skills/subtitle-extractslator/assets/bootstrap/* ./tmp/skill/subtitle-extractslator/assets/bootstrap/
```

## Runtime Package Build Examples

From repository root, build and pack one RID-specific NativeAOT NuGet package:

```powershell
./scripts/pack-rid-runtime.ps1 -Rid win-x64 -PackageVersion 0.1.0 -OutputRoot artifacts
```

```bash
pwsh -NoProfile -File ./scripts/pack-rid-runtime.ps1 -Rid linux-arm64 -PackageVersion 0.1.0 -OutputRoot artifacts
```

## Validation Checklist

1. `SKILL.md` exists with valid YAML frontmatter.
2. Skill folder name is kebab-case.
3. No skill ZIP or `subtitle-extractslator/assets/bin/` is published.
4. `assets/so-workflow/so-template.json` exists and is the execution basis.
5. The bootstrap resolves the selected channel's package for the current RID before invoking the CLI.
