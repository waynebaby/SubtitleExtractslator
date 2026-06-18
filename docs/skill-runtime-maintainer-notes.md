# Skill Runtime Maintainer Notes

Last updated: 2026-03-28

## Scope

This file keeps maintainer-facing runtime details that should not be embedded into skill prompt contract files under `.github/skills/subtitle-extractslator/references/`.

## Detailed MCP Setup Notes

Runtime acquisition rule:

1. The skill package is binary-free and does not ship `assets/bin/`.
2. Acquire `SubtitleExtractslator.Cli` from this repository's package index pages:

- `https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.released.md`
- `https://github.com/waynebaby/SubtitleExtractslator/blob/main/packages.beta.md`

1. Use an absolute DLL path outside the skill folder.

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
      "command": "dotnet",
      "args": ["C:\\runtime\\SubtitleExtractslator.Cli.dll", "--mode", "mcp"]
    }
  }
}
```

Cross-platform command path rule:

1. Use `dotnet` plus an absolute `SubtitleExtractslator.Cli.dll` path outside the skill directory.
2. Do not assume `.github/skills` or `.claude/skills` contains runtime binaries.
3. Package pages are the canonical runtime source; the skill folder only carries contracts and SO artifacts.

## Detailed Governed Runtime Entry

Canonical execution basis for the SO-enhanced skill:

1. Checked-in workflow authority: `.github/skills/subtitle-extractslator/assets/so-workflow/so-template.json`
2. Supporting planning source: `.github/skills/subtitle-extractslator/assets/so-workflow/skill-plan.md`
3. Runtime compile/run audit evidence should stay outside the skill folder.

Official SO guide refresh for governed maintenance:

1. Resolve the selected SO runtime directory.
2. Run `dotnet so.dll --guide --lang zh-cn`.

Component primitive guide for direct CLI runtime diagnostics:

1. Resolve external package DLL path.
2. Run `dotnet "<absolute-path>/SubtitleExtractslator.Cli.dll" --guide`.

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
