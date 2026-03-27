using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SubtitleExtractslator.Cli;

internal static class CliCommandRunner
{
    public static async Task<string> RunAsync(WorkflowOrchestrator orchestrator, AppOptions options)
    {
        using var envScope = RuntimeEnvironmentOverrides.Begin(RuntimeEnvironmentOverrides.Parse(options.OptionalString("env")));
        return options.Command?.ToLowerInvariant() switch
        {
            "probe" => JsonSerializer.Serialize(await orchestrator.ProbeAsync(
                options.Require("input"),
                options.Require("lang")), JsonOptions.Pretty),
            "opensubtitles-search" => JsonSerializer.Serialize(await orchestrator.SearchOpenSubtitlesAsync(
                options.Require("input"),
                options.Require("lang"),
                ResolveOpenSubtitlesSearchQueries(options),
                ResolveOpenSubtitlesCredentials(options, requireApiKey: true)), JsonOptions.Pretty),
            "opensubtitles-download" => JsonSerializer.Serialize(await RunOpenSubtitlesDownloadWithOptionsAsync(orchestrator, options), JsonOptions.Pretty),
            "extract" => JsonSerializer.Serialize(await orchestrator.ExtractSubtitleAsync(
                options.Require("input"),
                options.Require("out"),
                options.Arguments.TryGetValue("prefer", out var prefer) ? prefer : "en"), JsonOptions.Pretty),
            "translate" => JsonSerializer.Serialize(await RunTranslateWithOptionsAsync(orchestrator, options), JsonOptions.Pretty),
            "translate-batch" => JsonSerializer.Serialize(await RunTranslateBatchWithOptionsAsync(orchestrator, options), JsonOptions.Pretty),
            _ => AppOptions.HelpText
        };
    }

    private static async Task<WorkflowResult> RunTranslateWithOptionsAsync(WorkflowOrchestrator orchestrator, AppOptions options)
    {
        var retryOverride = options.OptionalInt("llm-retry-count");
        if (retryOverride is <= 0)
        {
            throw new InvalidOperationException("--llm-retry-count must be greater than 0.");
        }

        return await orchestrator.TranslateAsync(
            options.Require("input"),
            options.Require("lang"),
            options.Require("output"),
            options.OptionalInt("cues-per-group"),
            options.OptionalInt("body-size"),
            retryOverride,
            RuntimeEnvironmentOverrides.Parse(options.OptionalString("env")));
    }

    private static async Task<OpenSubtitlesDownloadResult> RunOpenSubtitlesDownloadWithOptionsAsync(
        WorkflowOrchestrator orchestrator,
        AppOptions options)
    {
        var candidateRank = options.OptionalInt("candidate-rank") ?? 1;
        if (candidateRank <= 0)
        {
            throw new InvalidOperationException("--candidate-rank must be greater than 0.");
        }

        return await orchestrator.DownloadOpenSubtitleAsync(
            options.Require("input"),
            options.Require("lang"),
            options.Require("output"),
            candidateRank,
            options.OptionalString("file-id"),
            ResolveOpenSubtitlesCredentials(options, requireApiKey: true)!);
    }

    private static async Task<BatchWorkflowResult> RunTranslateBatchWithOptionsAsync(WorkflowOrchestrator orchestrator, AppOptions options)
    {
        var inputListPath = options.Require("input-list");
        var targetLanguage = options.Require("lang");
        var outputDir = options.Require("output-dir");
        var outputSuffix = options.OptionalString("output-suffix") ?? $".{targetLanguage}.srt";

        if (string.IsNullOrWhiteSpace(outputSuffix))
        {
            throw new InvalidOperationException("--output-suffix cannot be empty.");
        }

        var retryOverride = options.OptionalInt("llm-retry-count");
        if (retryOverride is <= 0)
        {
            throw new InvalidOperationException("--llm-retry-count must be greater than 0.");
        }

        var envOverrides = RuntimeEnvironmentOverrides.Parse(options.OptionalString("env"));
        var inputs = ReadBatchInputPaths(inputListPath);
        Directory.CreateDirectory(outputDir);

        var results = new List<BatchWorkflowItemResult>(inputs.Count);
        var usedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            var resolvedOutput = BuildBatchOutputPath(input, outputDir, outputSuffix, usedOutputs);
            try
            {
                var workflow = await orchestrator.TranslateAsync(
                    input,
                    targetLanguage,
                    resolvedOutput,
                    options.OptionalInt("cues-per-group"),
                    options.OptionalInt("body-size"),
                    retryOverride,
                    envOverrides);

                results.Add(new BatchWorkflowItemResult(input, resolvedOutput, true, workflow.Status, workflow.Branch, null));
            }
            catch (Exception ex)
            {
                results.Add(new BatchWorkflowItemResult(input, resolvedOutput, false, null, null, ex.Message));
            }
        }

        return new BatchWorkflowResult(
            inputListPath,
            targetLanguage,
            outputDir,
            outputSuffix,
            results.Count,
            results.Count(x => x.Success),
            results.Count(x => !x.Success),
            results);
    }

    private static List<string> ReadBatchInputPaths(string listPath)
    {
        if (!File.Exists(listPath))
        {
            throw new InvalidOperationException($"Input list file not found: {listPath}");
        }

        var lines = File.ReadAllLines(listPath, Encoding.UTF8);
        var inputs = lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#", StringComparison.Ordinal))
            .ToList();

        if (inputs.Count == 0)
        {
            throw new InvalidOperationException("Input list file contains no valid paths.");
        }

        return inputs;
    }

    private static string BuildBatchOutputPath(string inputPath, string outputDir, string outputSuffix, HashSet<string> usedOutputs)
    {
        var fileNameBase = Path.GetFileNameWithoutExtension(inputPath);
        if (string.IsNullOrWhiteSpace(fileNameBase))
        {
            fileNameBase = "subtitle";
        }

        var candidate = Path.Combine(outputDir, fileNameBase + outputSuffix);
        if (usedOutputs.Add(candidate))
        {
            return candidate;
        }

        var index = 2;
        while (true)
        {
            var deduped = Path.Combine(outputDir, $"{fileNameBase}.{index}{outputSuffix}");
            if (usedOutputs.Add(deduped))
            {
                return deduped;
            }

            index++;
        }
    }

    private static string Require(this AppOptions options, string key)
    {
        if (options.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing required argument --{key}");
    }

    private static int? OptionalInt(this AppOptions options, string key)
    {
        if (!options.Arguments.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Invalid integer for --{key}: {value}");
    }

    private static string? OptionalString(this AppOptions options, string key)
    {
        if (!options.Arguments.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value;
    }

    private static OpenSubtitlesCredentials? ResolveOpenSubtitlesCredentials(AppOptions options, bool requireApiKey)
    {
        var apiKey = options.OptionalString("opensubtitles-api-key");
        var username = options.OptionalString("opensubtitles-username");
        var password = options.OptionalString("opensubtitles-password");
        var endpoint = options.OptionalString("opensubtitles-endpoint");
        var userAgent = options.OptionalString("opensubtitles-user-agent");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (requireApiKey)
            {
                throw new InvalidOperationException("Missing required argument --opensubtitles-api-key");
            }

            if (string.IsNullOrWhiteSpace(username)
                && string.IsNullOrWhiteSpace(password)
                && string.IsNullOrWhiteSpace(endpoint)
                && string.IsNullOrWhiteSpace(userAgent))
            {
                return null;
            }

            throw new InvalidOperationException("--opensubtitles-api-key is required when any OpenSubtitles credential/config parameter is provided.");
        }

        return new OpenSubtitlesCredentials(apiKey, username, password, endpoint, userAgent);
    }

    private static OpenSubtitlesSearchQueries ResolveOpenSubtitlesSearchQueries(AppOptions options)
    {
        var primary = options.Require("search-query-primary");
        var normalized = options.Require("search-query-normalized");
        return new OpenSubtitlesSearchQueries(primary, normalized);
    }
}
