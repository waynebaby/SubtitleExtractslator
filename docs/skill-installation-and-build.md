# SubtitleExtractslator Skill Build and Installation

This document follows Anthropic skill structure requirements.

Default distribution path is now source-only skill package plus runtime acquisition through this repository's package index pages. Repository main branch should not carry compiled binaries inside `.github/skills/subtitle-extractslator/`.

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
2. Acquire `SubtitleExtractslator.Cli` from this repository's package index pages:
   - `https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md`
   - `https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md`
3. If package feed is unavailable, use the fallback `.nupkg` link listed in the selected package index page.

## Install from Releases (Recommended)

1. Open repository Releases page and download `subtitle-extractslator-vX.Y.Z.zip`.
2. Unzip and keep the folder name `subtitle-extractslator` unchanged.
3. Verify the extracted skill folder contains no `assets/bin/` directory.
4. Acquire the runtime package from the selected package index page and run:
   - `dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide`

## Build Locally (Contributor Path)

From repository root, stage the source-only skill folder:

```powershell
New-Item -ItemType Directory -Path ".\tmp\skill\subtitle-extractslator\references" -Force | Out-Null
New-Item -ItemType Directory -Path ".\tmp\skill\subtitle-extractslator\assets\so-workflow" -Force | Out-Null
Copy-Item ".\.github\skills\subtitle-extractslator\SKILL.md" ".\tmp\skill\subtitle-extractslator\SKILL.md" -Force
Copy-Item ".\.github\skills\subtitle-extractslator\references\*" ".\tmp\skill\subtitle-extractslator\references" -Recurse -Force
Copy-Item ".\.github\skills\subtitle-extractslator\assets\so-workflow\*" ".\tmp\skill\subtitle-extractslator\assets\so-workflow" -Recurse -Force
```

```bash
mkdir -p ./tmp/skill/subtitle-extractslator/references
mkdir -p ./tmp/skill/subtitle-extractslator/assets/so-workflow
cp ./.github/skills/subtitle-extractslator/SKILL.md ./tmp/skill/subtitle-extractslator/SKILL.md
cp -R ./.github/skills/subtitle-extractslator/references/* ./tmp/skill/subtitle-extractslator/references/
cp -R ./.github/skills/subtitle-extractslator/assets/so-workflow/* ./tmp/skill/subtitle-extractslator/assets/so-workflow/
```

Optional zip output:

```powershell
Compress-Archive -Path ".\tmp\skill\subtitle-extractslator" -DestinationPath ".\tmp\subtitle-extractslator-local.zip" -Force
```

```bash
cd ./tmp
zip -r subtitle-extractslator-local.zip subtitle-extractslator
cd ..
```

## Runtime Package Build Examples

From repository root, build CLI runtime outside the skill folder:

```powershell
dotnet publish .\SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj -c Release -o .\artifacts\cli-runtime\win-x64
```

```bash
dotnet publish ./SubtitleExtractslator.Cli/SubtitleExtractslator.Cli.csproj -c Release -o ./artifacts/cli-runtime/linux-x64
```

## Upload Skill Package

1. Zip the skill folder `subtitle-extractslator`.
2. Upload in Claude settings skills section.
3. Enable the skill.

## Validation Checklist

1. `SKILL.md` exists with valid YAML frontmatter.
2. Skill folder name is kebab-case.
3. `subtitle-extractslator/assets/bin/` does not exist in the final skill package.
4. `assets/so-workflow/so-template.json` exists and is the execution basis.
5. Probe command succeeds when runtime is acquired from the package index page.
