# Command Reference

This file is aligned to actual `--help` output from the current CLI build.

## Usage

```text
SubtitleExtractslator.Cli --mode cli <command> [--key value ...] [--env "KEY=VALUE;KEY2=VALUE2"]
SubtitleExtractslator.Cli --mode mcp
SubtitleExtractslator.Cli --help
```

## Commands

```text
probe --input <mediaFile> --lang <targetLang>
subtitle-timing-check --input <mediaFile> --subtitle <subtitleFile.srt>
opensubtitles-search --input <mediaFile> --lang <targetLang> --search-query-primary <query> --search-query-normalized <query> [--opensubtitles-endpoint <url>] [--opensubtitles-user-agent <ua>]
opensubtitles-download --input <mediaFile> --lang <targetLang> --output <subtitleFile> [--candidate-rank <n> | --file-id <id>] [--opensubtitles-endpoint <url>] [--opensubtitles-user-agent <ua>]
subtitle auth login --api-key <key> --username <user> --password <pass> [--opensubtitles-endpoint <url>] [--opensubtitles-user-agent <ua>]
subtitle auth aquire
subtitle auth status
subtitle auth clear
extract --input <mediaFile> --out <subtitleFile> [--prefer en]
translate --input <subtitleFile.srt> --lang <targetLang> --output <subtitleFile>
    [--cues-per-group <n>] [--body-size <n>] [--llm-retry-count <n>]
translate-batch --input-list <paths.txt> --lang <targetLang> --output-dir <folder>
    [--output-suffix <suffix>] [--cues-per-group <n>] [--body-size <n>] [--llm-retry-count <n>]
```

## Global CLI options

```text
--env "KEY=VALUE;KEY2=VALUE2"  temporary per-command environment override
--help                          print this help text
```

 