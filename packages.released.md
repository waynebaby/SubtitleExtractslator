# SubtitleExtractslator Stable Packages

This channel is the stable production line published from `main`.

This page is the canonical runtime acquisition entry for the binary-free `.github/skills/subtitle-extractslator/` skill package.

## Install

```bash
dotnet add package SubtitleExtractslator.Cli --version <stable-version>
```

## Guide First

After restore or `.nupkg` extraction is available, resolve an absolute DLL path and run:

```bash
dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide
```

## SO Enhancement Note

The SO-enhanced workflow template currently ships on the Beta documentation path only. Use `packages.beta.md` for the current `so-template.json` contract story.

## GitHub Fallback

Use fallback only when package feed is unavailable.

- Latest stable fallback release: <https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-stable-latest>
- Latest stable `.latest.nupkg`: <https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-stable-latest/SubtitleExtractslator.Cli.latest.nupkg>
