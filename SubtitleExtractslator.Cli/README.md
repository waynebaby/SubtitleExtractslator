# SubtitleExtractslator.Cli

`SubtitleExtractslator.Cli` is the portable .NET CLI runtime for SubtitleExtractslator workflows.

It provides deterministic subtitle probing, extraction, OpenSubtitles candidate lookup/download, and context-aware translation while preserving SRT timing and structure.

## Channel And Installation

Stable and beta package indexes:

- Stable index: https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md
- Stable index (zh-CN): https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md
- Beta index: https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md
- Beta index (zh-CN): https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md

Typical install command:

```bash
dotnet add package SubtitleExtractslator.Cli --version <VERSION>
```

If the package feed is unavailable, use GitHub fallback links from the package indexes above.

## Guide-First Entry

After obtaining the runtime, use guide mode as the authoritative entry point:

```bash
dotnet SubtitleExtractslator.Cli.dll --guide
```

The guide prints channel information, command entry points, and fallback locations.

## Typical Command Entry

```bash
dotnet SubtitleExtractslator.Cli.dll --mode cli probe --input "movie.mkv" --lang zh
```

```bash
dotnet SubtitleExtractslator.Cli.dll --mode mcp
```

## Relationship To Skill

The skill package remains discoverable via repository installation and acts as a routing layer. Runtime command truth should come from CLI guide output rather than duplicated skill-side runtime instructions.
