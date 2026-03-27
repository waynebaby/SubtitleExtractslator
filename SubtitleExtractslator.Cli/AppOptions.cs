namespace SubtitleExtractslator.Cli;

internal sealed record AppOptions(
    AppMode Mode,
    string? Command,
    IReadOnlyDictionary<string, string> Arguments)
{
    public static string HelpText => """
SubtitleExtractslator CLI

Usage:
        SubtitleExtractslator.Cli --mode cli <command> [--key value ...] [--env "KEY=VALUE;KEY2=VALUE2"]
        SubtitleExtractslator.Cli --mode mcp
        SubtitleExtractslator.Cli --help

Commands:
  probe --input <mediaFile> --lang <targetLang>
    opensubtitles-search --input <mediaFile> --lang <targetLang> --search-query-primary <query> --search-query-normalized <query> --opensubtitles-api-key <key> [--opensubtitles-username <user>] [--opensubtitles-password <pass>] [--opensubtitles-endpoint <url>] [--opensubtitles-user-agent <ua>]
    opensubtitles-download --input <mediaFile> --lang <targetLang> --output <subtitleFile> --opensubtitles-api-key <key> [--candidate-rank <n>] [--file-id <id>] [--opensubtitles-username <user>] [--opensubtitles-password <pass>] [--opensubtitles-endpoint <url>] [--opensubtitles-user-agent <ua>]
  extract --input <mediaFile> --out <subtitleFile> [--prefer en]
        run-workflow --input <mediaFile> --lang <targetLang> --output <subtitleFile> [--cues-per-group <n>] [--body-size <n>] [--llm-retry-count <n>] [--mux-output <mediaFile>] [--opensubtitles-api-key <key>] [--opensubtitles-username <user>] [--opensubtitles-password <pass>] [--opensubtitles-endpoint <url>] [--opensubtitles-user-agent <ua>]
    run-workflow-batch --input-list <paths.txt> --lang <targetLang> --output-dir <folder> [--output-suffix <suffix>] [--cues-per-group <n>] [--body-size <n>] [--llm-retry-count <n>]

Global CLI options:
        --env "KEY=VALUE;KEY2=VALUE2"  temporary per-command environment override
        --help                          print this help text
""";

    public static AppOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();
        var multiTokenOptionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "input",
            "input-list",
            "output",
            "out",
            "mux-output",
            "output-dir",
            "opensubtitles-user-agent"
        };

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(token);
                continue;
            }

            var key = token[2..];
            var value = "true";
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
                if (multiTokenOptionKeys.Contains(key))
                {
                    while (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        value += " " + args[++i];
                    }
                }
            }

            map[key] = value;
        }

        if (positional.Count > 0)
        {
            map["command"] = positional[0];
        }

        var mode = map.TryGetValue("mode", out var modeValue) && modeValue.Equals("mcp", StringComparison.OrdinalIgnoreCase)
            ? AppMode.Mcp
            : AppMode.Cli;

        map.TryGetValue("command", out var command);
        return new AppOptions(mode, command, map);
    }
}
