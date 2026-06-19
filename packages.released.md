# SubtitleExtractslator Stable Packages

This channel is the stable production line published from `main`.

This page is the canonical runtime acquisition entry for the binary-free `.github/skills/subtitle-extractslator/` skill package.

## Install

```bash
dotnet add package SubtitleExtractslator.Cli --version <stable-version>
```

## Install Skill Package

Use the packaged skill zip instead of repo-root discovery:

```bash
npx skills add https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-stable-latest/subtitle-extractslator-skill.zip
```

## Guide First

After restore or `.nupkg` extraction is available, resolve an absolute DLL path and run:

```bash
dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide
```

## SO Enhancement Note

This skill has been enhanced by Loom SO and is now SO-exclusive governed.

- Execution authority: SO only
- Official run: `dotnet so.dll run --workflow-file <skill-path>/assets/so-workflow/so-template.json`
- Official resume: `dotnet so.dll resume --workflow-file <runtime-workflow-copy>.json --result-file <external-result>.json`
- Direct CLI and direct MCP: runtime primitives only (not official skill execution history)

For released governance references:

- SO package index: <https://github.com/waynebaby/Techne-Loom/blob/main/packages.released.md>
- SO guide: <https://github.com/waynebaby/Techne-Loom/blob/main/docs/en/reference/products/so-guide.md>

## GitHub Fallback

Use fallback only when package feed is unavailable.

- Latest stable fallback release: <https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-stable-latest>
- Latest stable skill zip: <https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-stable-latest/subtitle-extractslator-skill.zip>
- Latest stable `.latest.nupkg`: <https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-stable-latest/SubtitleExtractslator.Cli.latest.nupkg>
