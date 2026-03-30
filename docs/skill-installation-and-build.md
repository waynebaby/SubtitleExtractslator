# SubtitleExtractslator Skill Build and Installation

This document follows Anthropic skill structure requirements.

Default distribution path is now GitHub Releases. Repository main branch should not carry compiled binaries.

## Skill Folder

Skill root:

- `subtitle-extractslator/` inside the release zip package

Required file:

- `subtitle-extractslator/SKILL.md`

Optional resources used here:

- `subtitle-extractslator/references/commands.md`
- `subtitle-extractslator/references/troubleshooting.md`
- `subtitle-extractslator/assets/bin/<rid>/SubtitleExtractslator.Cli[.exe]`

No README is placed inside the skill folder.

## Install from Releases (Recommended)

1. Open repository Releases page and download `subtitle-extractslator-vX.Y.Z.zip`.
2. Unzip and keep the folder name `subtitle-extractslator` unchanged.
3. Verify binaries exist in:
   - `subtitle-extractslator/assets/bin/win-x64/`
   - `subtitle-extractslator/assets/bin/linux-x64/`
   - `subtitle-extractslator/assets/bin/osx-arm64/`

## Build Locally (Contributor Path)

From repository root, build all runtime binaries into a staging skill folder:

```powershell
.\scripts\publish-skill-binaries.ps1 -OutputRoot ".\tmp\skill\subtitle-extractslator\assets\bin"
```

```bash
pwsh ./scripts/publish-skill-binaries.ps1 -OutputRoot "./tmp/skill/subtitle-extractslator/assets/bin"
```

Then copy the skill source files into the same staging folder:

```powershell
New-Item -ItemType Directory -Path ".\tmp\skill\subtitle-extractslator\references" -Force | Out-Null
Copy-Item ".\subtitle-extractslator\SKILL.md" ".\tmp\skill\subtitle-extractslator\SKILL.md" -Force
Copy-Item ".\subtitle-extractslator\references\*" ".\tmp\skill\subtitle-extractslator\references" -Recurse -Force
```

```bash
mkdir -p ./tmp/skill/subtitle-extractslator/references
cp ./subtitle-extractslator/SKILL.md ./tmp/skill/subtitle-extractslator/SKILL.md
cp -R ./subtitle-extractslator/references/* ./tmp/skill/subtitle-extractslator/references/
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

## Direct dotnet publish examples

From repository root:

```powershell
dotnet publish .\SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o .\tmp\skill\subtitle-extractslator\assets\bin\win-x64
```

```bash
dotnet publish ./SubtitleExtractslator.Cli/SubtitleExtractslator.Cli.csproj -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true -o ./tmp/skill/subtitle-extractslator/assets/bin/linux-x64
```

## Optional Additional Runtime Builds

Linux x64:

```powershell
dotnet publish .\SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true -o .\tmp\skill\subtitle-extractslator\assets\bin\linux-x64
```

```bash
dotnet publish ./SubtitleExtractslator.Cli/SubtitleExtractslator.Cli.csproj -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true -o ./tmp/skill/subtitle-extractslator/assets/bin/linux-x64
```

macOS arm64:

```powershell
dotnet publish .\SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true -o .\tmp\skill\subtitle-extractslator\assets\bin\osx-arm64
```

```bash
dotnet publish ./SubtitleExtractslator.Cli/SubtitleExtractslator.Cli.csproj -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true -o ./tmp/skill/subtitle-extractslator/assets/bin/osx-arm64
```

## Upload Skill Package

1. Zip the skill folder `subtitle-extractslator`.
2. Upload in Claude settings skills section.
3. Enable the skill.

## Validation Checklist

1. `SKILL.md` exists with valid YAML frontmatter.
2. Skill folder name is kebab-case.
3. CLI binary exists in assets bin path.
4. Probe command succeeds on test subtitle file.
5. Full workflow command emits SRT output.
