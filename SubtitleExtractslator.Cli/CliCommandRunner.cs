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
            "subtitle-timing-check" => JsonSerializer.Serialize(await orchestrator.CheckSubtitleTimingAsync(
                options.Require("input"),
                options.Require("subtitle")), JsonOptions.Pretty),
            "opensubtitles-search" => JsonSerializer.Serialize(await orchestrator.SearchOpenSubtitlesAsync(
                options.Require("input"),
                options.Require("lang"),
                ResolveOpenSubtitlesSearchQueries(options),
                ResolveOpenSubtitlesCredentials(options)), JsonOptions.Pretty),
            "opensubtitles-download" => JsonSerializer.Serialize(await RunOpenSubtitlesDownloadWithOptionsAsync(orchestrator, options), JsonOptions.Pretty),
            "subtitle" => JsonSerializer.Serialize(await RunSubtitleAuthCommandAsync(options), JsonOptions.Pretty),
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
            ResolveOpenSubtitlesCredentials(options));
    }

    private static Task<AuthCommandResult> RunSubtitleAuthCommandAsync(AppOptions options)
    {
        var group = options.OptionalString("command-2");
        if (!string.Equals(group, "auth", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Unsupported subtitle command. Use: subtitle auth <login|aquire|status|clear>.");
        }

        var action = options.OptionalString("command-3")?.ToLowerInvariant();
        return action switch
        {
            "login" => Task.FromResult(RunSubtitleAuthLogin(options)),
            "aquire" => Task.FromResult(RunSubtitleAuthAquire(options)),
            "status" => Task.FromResult(OpenSubtitlesAuthStore.Status()),
            "clear" => Task.FromResult(OpenSubtitlesAuthStore.Clear()),
            _ => throw new InvalidOperationException("Unsupported subtitle auth action. Use: subtitle auth <login|aquire|status|clear>.")
        };
    }

    private static AuthCommandResult RunSubtitleAuthLogin(AppOptions options)
    {
        var apiKey = ResolveRequiredAuthField(options, "api-key", "OpenSubtitles API key", secret: false);
        var username = ResolveRequiredAuthField(options, "username", "OpenSubtitles username", secret: false);
        var password = ResolveRequiredAuthField(options, "password", "OpenSubtitles password", secret: true);
        var endpoint = options.OptionalString("opensubtitles-endpoint");
        var userAgent = options.OptionalString("opensubtitles-user-agent");

        var credentials = new OpenSubtitlesCredentials(apiKey, username, password, endpoint, userAgent);
        var accessor = OpenSubtitlesAccessor.Create(credentials)
            ?? throw OpenSubtitlesAuthException.ReloginRequired("OpenSubtitles auth login failed.");
        try
        {
            accessor.ValidateSessionAsync().GetAwaiter().GetResult();
        }
        catch (OpenSubtitlesAuthException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw OpenSubtitlesAuthException.ReloginRequired($"OpenSubtitles auth login failed. {ex.Message}");
        }

        return OpenSubtitlesAuthStore.Login(apiKey, username, password, endpoint, userAgent);
    }

    private static AuthCommandResult RunSubtitleAuthAquire(AppOptions options)
    {
        var credentials = ResolveOpenSubtitlesCredentials(options);
        var accessor = OpenSubtitlesAccessor.Create(credentials)
            ?? throw OpenSubtitlesAuthException.ReloginRequired("OpenSubtitles sk auth is empty.");

        try
        {
            accessor.ValidateSessionAsync().GetAwaiter().GetResult();
        }
        catch (OpenSubtitlesAuthException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw OpenSubtitlesAuthException.ReloginRequired($"OpenSubtitles auth aquire failed. {ex.Message}");
        }

        return new AuthCommandResult(
            "aquire",
            true,
            "OpenSubtitles auth acquired successfully.",
            true,
            OpenSubtitlesAuthStore.CachePath);
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

    private static OpenSubtitlesCredentials ResolveOpenSubtitlesCredentials(AppOptions options)
    {
        if (options.OptionalString("opensubtitles-api-key") is not null)
        {
            throw new InvalidOperationException(
                "--opensubtitles-api-key is no longer supported. Run subtitle auth login to store api-key and retry.");
        }

        var endpoint = options.OptionalString("opensubtitles-endpoint");
        var userAgent = options.OptionalString("opensubtitles-user-agent");
        return OpenSubtitlesAuthStore.Acquire(endpoint, userAgent);
    }

    private static OpenSubtitlesSearchQueries ResolveOpenSubtitlesSearchQueries(AppOptions options)
    {
        var primary = options.Require("search-query-primary");
        var normalized = options.Require("search-query-normalized");
        return new OpenSubtitlesSearchQueries(primary, normalized);
    }

    private static string ResolveRequiredAuthField(AppOptions options, string key, string prompt, bool secret)
    {
        var value = options.OptionalString(key);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            throw new InvalidOperationException(
                $"Missing required argument --{key}. Non-interactive mode cannot prompt. Provide --api-key, --username, and --password explicitly.");
        }

        Console.Write($"{prompt}: ");
        var entered = secret ? ReadSecretFromConsole() : Console.ReadLine();
        if (string.IsNullOrWhiteSpace(entered))
        {
            throw new InvalidOperationException($"{prompt} cannot be empty.");
        }

        return entered.Trim();
    }

    private static string ReadSecretFromConsole()
    {
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                }

                continue;
            }

            if (key.KeyChar != '\u0000')
            {
                chars.Add(key.KeyChar);
            }
        }

        return new string(chars.ToArray());
    }
}
