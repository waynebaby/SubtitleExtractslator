# SubtitleExtractslator Stable Packages

This channel is the stable production line published from `main`.

## Install

```bash
dotnet add package SubtitleExtractslator.Cli --version <stable-version>
```

## Guide First

After restore/build output is available, run:

```bash
dotnet SubtitleExtractslator.Cli.dll --guide
```

## GitHub Fallback

Use fallback only when package feed is unavailable.

- Latest stable fallback release: https://github.com/waynebaby/SubtitleExtractslator/releases/tag/nuget-stable-latest
- Latest stable `.latest.nupkg`: https://github.com/waynebaby/SubtitleExtractslator/releases/download/nuget-stable-latest/SubtitleExtractslator.Cli.latest.nupkg
