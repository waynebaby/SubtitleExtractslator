# SubtitleExtractslator.Cli

`SubtitleExtractslator.Cli` is the source project for SubtitleExtractslator's per-platform NativeAOT CLI runtime packages.

It provides deterministic subtitle probing, extraction, OpenSubtitles candidate lookup/download, and context-aware translation while preserving SRT timing and structure.

## Channel And Installation

Stable and beta package indexes:

- Stable index: <https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md>
- Stable index (zh-CN): <https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.zh-CN.md>
- Beta index: <https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md>
- Beta index (zh-CN): <https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.zh-CN.md>

Published packages use the IDs `SubtitleExtractslator.Cli.<rid>` and contain one platform-native executable under `tools/<rid>/`. Stable and Beta release sets use one shared version across the supported RIDs. The skill's Python bootstrap resolves the latest package for the host RID and downloads it only when the versioned cache is missing.

```bash
python3 .github/skills/subtitle-extractslator/assets/bootstrap/restore_runtime.py --channel stable
```

The native CLI does not require the .NET runtime. Python 3 and HTTPS access to NuGet are required for bootstrap; FFmpeg remains a separate media-processing dependency.

If the package feed is unavailable, use GitHub fallback links from the package indexes above.

## Guide-First Entry

The bootstrap prints the absolute native executable path. Use that executable for guide mode and subsequent CLI/MCP commands:

```bash
<absolute-path>/SubtitleExtractslator.Cli --guide
```

The guide prints channel information, command entry points, and fallback locations.

## Typical Command Entry

```bash
<absolute-path>/SubtitleExtractslator.Cli --mode cli probe --input "movie.mkv" --lang zh
```

```bash
<absolute-path>/SubtitleExtractslator.Cli --mode mcp
```

## Relationship To Skill

The skill source remains discoverable via repository installation and acts as a routing layer. It stays binary-free and does not ship runtime DLLs, platform bins, or a ZIP release. Native runtime packages are published separately for each supported RID.
