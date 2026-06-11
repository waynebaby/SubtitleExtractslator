# SubtitleExtractslator Beta Packages

This channel is the prerelease line published from `development`.

This page is the canonical runtime acquisition entry for the binary-free `.github/skills/subtitle-extractslator/` skill package.

## Install

```bash
dotnet add package SubtitleExtractslator.Cli --version <beta-version>
```

## Guide First

After restore or `.nupkg` extraction is available, resolve an absolute DLL path and run:

```bash
dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide
```

## SO-Enhanced Skill Contract

1. Execution basis: `.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json`
2. Compile input only: `.github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md`
3. Audit artifacts: `.github/skills/subtitle-extractslator/assets/so-workflow/audit/`

## GitHub Fallback

Use fallback only when package feed is unavailable.

- Latest beta fallback release: <https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-beta-latest>
- Latest beta `.latest.nupkg`: <https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-beta-latest/SubtitleExtractslator.Cli.latest.nupkg>
