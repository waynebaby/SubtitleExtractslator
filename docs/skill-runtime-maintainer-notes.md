# Skill Runtime Maintainer Notes

Last updated: 2026-03-28

## Scope

This file keeps maintainer-facing runtime details that should not be embedded into skill prompt contract files under `subtitle-extractslator/references/`.

## Detailed MCP Setup Notes

Agent MCP config target guidance:
1. GitHub Copilot commonly uses `./.vscode/mcp.json`.
2. Claude Code/OpenClaw/Codex use client-specific MCP config locations.
3. If the client path is unknown, ask user for the exact config file before writing.

Windows snippet example:

```json
{
  "servers": {
    "subtitle-extractslator": {
      "type": "stdio",
      "command": "E:\\code\\g\\SubtitleExtractslator\\.github\\skills\\subtitle-extractslator\\assets\\bin\\win-x64\\SubtitleExtractslator.Cli.exe",
      "args": ["--mode", "mcp"]
    }
  }
}
```

Cross-platform command path rule:
1. Use an absolute binary path under active skill directory (`.github/skills` or `.claude/skills`).
2. Windows executable name: `SubtitleExtractslator.Cli.exe`.
3. Linux/macOS executable name: `SubtitleExtractslator.Cli`.

## Detailed CLI Runtime Matrix

Platform binaries:
1. Windows x64: `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe`
2. Windows ARM64: `./assets/bin/win-arm64/SubtitleExtractslator.Cli.exe`
3. Linux x64: `./assets/bin/linux-x64/SubtitleExtractslator.Cli`
4. Linux musl x64: `./assets/bin/linux-musl-x64/SubtitleExtractslator.Cli`
5. Linux ARM64: `./assets/bin/linux-arm64/SubtitleExtractslator.Cli`
6. Linux musl ARM64: `./assets/bin/linux-musl-arm64/SubtitleExtractslator.Cli`
7. Linux ARM (32-bit): `./assets/bin/linux-arm/SubtitleExtractslator.Cli`
8. macOS ARM64: `./assets/bin/osx-arm64/SubtitleExtractslator.Cli`
9. macOS x64: `./assets/bin/osx-x64/SubtitleExtractslator.Cli`

## Bitmap Subtitle Branch Internals (CLI)

When selected subtitle codec is bitmap (`hdmv_pgs_subtitle` or `dvd_subtitle`), extract path is:
1. SUP export
2. built-in SUP decode to PNG + timeline
3. OCR
4. SRT emit

Runtime details:
1. Render-overlay screenshot path is disabled; PNG frames come from SUP conversion.
2. Temp artifacts path default: `Path.GetTempPath()/SubtitleExtractslator/pgs`.
3. Temp root override env: `SUBTITLEEXTRACTSLATOR_TEMPDIR`.
4. SUP-to-PNG decode is built-in C# (no external converter command).
5. OCR is built-in C# and calls local OpenAI-compatible chat completion endpoint.

OCR environment defaults:
1. `LLM_ENDPOINT`: `http://localhost:1234/v1/chat/completions`
2. `LLM_MODEL`: `qwen3.5-9b-uncensored-hauhaucs-aggressive`
3. `SUBTITLEEXTRACTSLATOR_PGS_OCR_TIMEOUT_SECONDS`: default `120`, clamp `5..600`
4. `SUBTITLEEXTRACTSLATOR_PGS_OCR_MAX_CUES`: default `160`, clamp `1..2000`

Reasoning behavior for OCR:
1. ignores `LLM_REASONING`
2. fallback order: `off` -> `low` -> unset