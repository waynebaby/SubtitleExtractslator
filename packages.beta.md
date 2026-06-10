# SubtitleExtractslator Beta Packages

This channel is the prerelease line published from `development`.

## Install

```bash
dotnet add package SubtitleExtractslator.Cli --version <beta-version>
```

## Guide First

After restore/build output is available, run:

```bash
dotnet SubtitleExtractslator.Cli.dll --guide
```

## GitHub Fallback

Use fallback only when package feed is unavailable.

- Latest beta fallback release: https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-beta-latest
- Latest beta `.latest.nupkg`: https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-beta-latest/SubtitleExtractslator.Cli.latest.nupkg
