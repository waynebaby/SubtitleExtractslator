# SubtitleExtractslator Skill Build and Installation

This document follows Anthropic skill structure requirements and explains how to build the CLI into the skill folder.

## Skill Folder

Skill root:
- `.github/skills/subtitle-extractslator/`

Required file:
- `.github/skills/subtitle-extractslator/SKILL.md`

Optional resources used here:
- `.github/skills/subtitle-extractslator/references/commands.md`
- `.github/skills/subtitle-extractslator/references/troubleshooting.md`
- `.github/skills/subtitle-extractslator/assets/bin/win-x64/SubtitleExtractslator.Cli.exe`

No README is placed inside the skill folder.

## Build to Skill Output Path

From repository root:

```powershell
dotnet publish .\SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -o .\.github\skills\subtitle-extractslator\assets\bin\win-x64
```

## Optional Additional Runtime Builds

Linux x64:

```powershell
dotnet publish .\SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj -c Release -r linux-x64 -p:PublishSingleFile=true -p:SelfContained=true -o .\.github\skills\subtitle-extractslator\assets\bin\linux-x64
```

macOS arm64:

```powershell
dotnet publish .\SubtitleExtractslator.Cli\SubtitleExtractslator.Cli.csproj -c Release -r osx-arm64 -p:PublishSingleFile=true -p:SelfContained=true -o .\.github\skills\subtitle-extractslator\assets\bin\osx-arm64
```

## Upload to Claude

1. Zip the skill folder `.github/skills/subtitle-extractslator`.
2. Upload in Claude settings skills section.
3. Enable the skill.

## Validation Checklist

1. `SKILL.md` exists with valid YAML frontmatter.
2. Skill folder name is kebab-case.
3. CLI binary exists in assets bin path.
4. Probe command succeeds on test subtitle file.
5. Full workflow command emits SRT output.
