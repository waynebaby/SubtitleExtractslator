using System.Diagnostics;
using System.ComponentModel;
using Azure.Core;
using Azure.Identity;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace SubtitleExtractslator.Cli;

internal enum AppMode
{
    Cli,
    Mcp
}

internal enum ModeContext
{
    Cli,
    Mcp
}

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

Commands:
  probe --input <mediaFile> --lang <targetLang>
  opensubtitles-search --input <mediaFile> --lang <targetLang>
  extract --input <mediaFile> --out <subtitleFile> [--prefer en]
        run-workflow --input <mediaFile> --lang <targetLang> --output <subtitleFile> [--cues-per-group <n>] [--body-size <n>] [--llm-retry-count <n>]

Notes:
  In MCP mode, translation source policy is: sampling first, then external provider fallback.
  In CLI mode, translation source policy is: external provider only.
    Parameter override precedence:
        command parameter > --env overrides > process environment variable > built-in default.
    Grouping knobs:
        --cues-per-group (or env SUBTITLEEXTRACTSLATOR_CUES_PER_GROUP), default 10
        --body-size (or env SUBTITLEEXTRACTSLATOR_TRANSLATION_BODY_SIZE), default 3
    LLM knobs:
        --llm-retry-count (or env LLM_RETRY_COUNT), default 3
""";

    public static AppOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();

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
                options.Require("lang")), JsonOptions.Pretty),
            "extract" => JsonSerializer.Serialize(await orchestrator.ExtractSubtitleAsync(
                options.Require("input"),
                options.Require("out"),
                options.Arguments.TryGetValue("prefer", out var prefer) ? prefer : "en"), JsonOptions.Pretty),
            "run-workflow" => JsonSerializer.Serialize(await RunWorkflowWithOptionsAsync(orchestrator, options), JsonOptions.Pretty),
            _ => AppOptions.HelpText
        };
    }

    private static async Task<WorkflowResult> RunWorkflowWithOptionsAsync(WorkflowOrchestrator orchestrator, AppOptions options)
    {
        var retryOverride = options.OptionalInt("llm-retry-count");
        if (retryOverride is <= 0)
        {
            throw new InvalidOperationException("--llm-retry-count must be greater than 0.");
        }

        return await orchestrator.RunWorkflowAsync(
            options.Require("input"),
            options.Require("lang"),
            options.Require("output"),
            options.OptionalInt("cues-per-group"),
            options.OptionalInt("body-size"),
            retryOverride,
            RuntimeEnvironmentOverrides.Parse(options.OptionalString("env")));
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
}

internal sealed class WorkflowOrchestrator
{
    private readonly SubtitleOperations _ops = new();
    private readonly TranslationPipeline _translator;

    public WorkflowOrchestrator(ModeContext modeContext)
    {
        _translator = new TranslationPipeline(modeContext, new ExternalTranslationProvider(), new SamplingTranslationProvider());
    }

    public Task<ProbeResult> ProbeAsync(string input, string targetLanguage)
        => _ops.ProbeTracksAsync(input, targetLanguage);

    public Task<OpenSubtitlesResult> SearchOpenSubtitlesAsync(string input, string targetLanguage)
        => _ops.SearchOpenSubtitlesAsync(input, targetLanguage);

    public Task<ExtractionResult> ExtractSubtitleAsync(string input, string output, string preferredLanguage)
        => _ops.ExtractSubtitleAsync(input, output, preferredLanguage);

    public async Task<WorkflowResult> RunWorkflowAsync(
        string input,
        string targetLanguage,
        string output,
        int? cuesPerGroupOverride = null,
        int? bodySizeOverride = null,
        int? llmRetryCountOverride = null,
        IReadOnlyDictionary<string, string>? envOverrides = null)
    {
        using var workflowScope = CliRuntimeLog.BeginScope("workflow", $"Start run_workflow input={input} target={targetLanguage} output={output}");
        using var envScope = RuntimeEnvironmentOverrides.Begin(envOverrides);
        using var historyScope = ErrorSnapshotWriter.BeginTranslateHistoryScope(output);
        using var llmRetryScope = LlmRuntimeOverrides.BeginRetryCountScope(llmRetryCountOverride);
        var probe = await ProbeAsync(input, targetLanguage);
        CliRuntimeLog.Info("workflow", $"Probe completed. tracks={probe.Tracks.Count} hasTarget={probe.HasTargetLanguage}");
        if (probe.HasTargetLanguage)
        {
            CliRuntimeLog.Info("workflow", "Target subtitle already exists. Workflow exits early.");
            return new WorkflowResult("completed", "target-track-exists", output, new List<GroupTranslationResult>());
        }

        var openResult = await SearchOpenSubtitlesAsync(input, targetLanguage);
        CliRuntimeLog.Info("workflow", $"OpenSubtitles candidates={openResult.Candidates.Count}");
        var accepted = false;
        if (openResult.Candidates.Count > 0)
        {
            if (_translator.ModeContext == ModeContext.Cli)
            {
                CliRuntimeLog.Info("workflow", "Prompting for OpenSubtitles candidate adoption.");
                accepted = AskUserYesNo("OpenSubtitles has candidates. Use candidate #1? [y/N]: ");
                CliRuntimeLog.Info("workflow", $"Candidate accepted={accepted}");
            }
        }

        var subtitlePath = output;
        if (!accepted)
        {
            CliRuntimeLog.Info("workflow", "Using local extraction branch (preferred language: en).");
            var provisionalPath = BuildTemporaryExtractionPath(output, "source");
            var extract = await ExtractSubtitleAsync(input, provisionalPath, "en");
            var sourceLanguage = NormalizeLanguageToken(extract.SelectedLanguage);
            var languageNamedPath = BuildTemporaryExtractionPath(output, sourceLanguage);
            if (!extract.OutputPath.Equals(languageNamedPath, StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(languageNamedPath))
                {
                    File.Delete(languageNamedPath);
                }

                File.Move(extract.OutputPath, languageNamedPath);
            }

            subtitlePath = languageNamedPath;
            CliRuntimeLog.Info("workflow", $"Extraction completed. sourceLanguage={sourceLanguage} subtitlePath={subtitlePath}");
        }
        else
        {
            CliRuntimeLog.Info("workflow", "Using OpenSubtitles adoption branch.");
        }

        var cues = await _ops.LoadSrtAsync(subtitlePath);
        CliRuntimeLog.Info("workflow", $"SRT loaded. cues={cues.Count}");
        var cuesPerGroup = ResolveCuesPerGroup(cuesPerGroupOverride);
        var bodySize = ResolveTranslationBodySize(bodySizeOverride);
        CliRuntimeLog.Info("workflow", $"Grouping settings. cuesPerGroup={cuesPerGroup} bodySize={bodySize}");
        var groups = GroupingEngine.Group(cues, cuesPerGroup);
        CliRuntimeLog.Info("workflow", $"Grouping completed. groups={groups.Count} groupSizes=[{string.Join(',', groups.Select(g => g.Cues.Count))}]");
        var units = BuildTranslationUnits(groups, bodySize);
        CliRuntimeLog.Info("workflow", $"Translation units={units.Count} bodyGroupSize={bodySize}");
        var degree = ResolveTranslationParallelism();
        CliRuntimeLog.Info("workflow", $"Translation parallelism={degree}");
        var groupResults = await TranslateUnitsAsync(units, targetLanguage, degree);

        var merged = groupResults
            .OrderBy(x => x.GroupIndex)
            .SelectMany(x => x.Cues.OrderBy(c => c.Index))
            .ToList();
        CliRuntimeLog.Info("workflow", $"Merging groups completed. merged cues={merged.Count}");
        await _ops.SaveSrtAsync(output, merged);
        CliRuntimeLog.Info("workflow", $"Output written: {output}");

        return new WorkflowResult("completed", accepted ? "opensubtitles-adopted" : "local-extraction", output, groupResults);
    }

    private static List<TranslationUnit> BuildTranslationUnits(List<SubtitleGroup> groups, int bodySize)
    {
        var units = new List<TranslationUnit>();
        if (groups.Count == 0)
        {
            return units;
        }

        bodySize = Math.Max(1, bodySize);
        for (var i = 0; i < groups.Count; i += bodySize)
        {
            var bodyGroups = groups.Skip(i).Take(bodySize).ToList();
            var contextGroups = new List<SubtitleGroup>();

            if (i - 1 >= 0)
            {
                contextGroups.Add(groups[i - 1]);
            }

            contextGroups.AddRange(bodyGroups);

            if (i + bodySize < groups.Count)
            {
                contextGroups.Add(groups[i + bodySize]);
            }

            units.Add(new TranslationUnit(
                bodyGroups[0].GroupIndex,
                bodyGroups,
                contextGroups));
        }

        return units;
    }

    private async Task<List<GroupTranslationResult>> TranslateUnitsAsync(List<TranslationUnit> units, string targetLanguage, int degree)
    {
        if (units.Count == 0)
        {
            return new List<GroupTranslationResult>();
        }

        var total = units.Count;
        var running = 0;
        var completed = 0;
        CliRuntimeLog.Info("workflow", $"Translation progress init. total={total} queued={total} running=0 completed=0 parallelism={degree}");

        var results = new GroupTranslationResult[units.Count];
        using var limiter = new SemaphoreSlim(degree);
        var tasks = units.Select((_, i) => TranslateUnitAtIndexAsync(i)).ToList();
        await Task.WhenAll(tasks);
        CliRuntimeLog.Info("workflow", $"Translation progress final. total={total} queued=0 running=0 completed={completed}");
        return results.ToList();

        async Task TranslateUnitAtIndexAsync(int index)
        {
            await limiter.WaitAsync();
            var runningNow = Interlocked.Increment(ref running);
            var completedNow = Volatile.Read(ref completed);
            var queuedNow = total - runningNow - completedNow;
            CliRuntimeLog.Info("workflow", $"Progress update. total={total} queued={queuedNow} running={runningNow} completed={completedNow} started={index + 1}/{total}");

            var status = "done";
            try
            {
                var unit = units[index];
                var bodyCueCount = unit.BodyGroups.Sum(g => g.Cues.Count);
                var bodyLineCount = unit.BodyGroups.Sum(g => g.Cues.Sum(c => c.Lines.Count));
                var bodyIndices = unit.BodyGroups.Select(x => x.GroupIndex).ToList();
                var contextIndices = unit.ContextGroups.Select(x => x.GroupIndex).ToList();
                var sideContextIndices = contextIndices.Except(bodyIndices).ToList();
                CliRuntimeLog.Info(
                    "workflow",
                    $"翻译组 {FormatGroupSymbols(bodyIndices)} 上下文 {FormatGroupSymbols(sideContextIndices)} 意译 {FormatGroupSymbols(contextIndices)} 逐行翻译 {FormatGroupSymbols(bodyIndices)}");
                CliRuntimeLog.Info("workflow", $"Unit {index + 1}/{units.Count} start. bodyCues={bodyCueCount} bodyLines={bodyLineCount}");

                var mainGroup = new SubtitleGroup(
                    unit.MainGroupIndex,
                    unit.BodyGroups.SelectMany(x => x.Cues).ToList());
                var translated = await _translator.TranslateGroupAsync(mainGroup, unit.ContextGroups, targetLanguage);
                results[index] = translated;
                CliRuntimeLog.Info("workflow", $"Unit {index + 1}/{units.Count} done. translated cues={translated.Cues.Count}");
            }
            catch
            {
                status = "failed";
                throw;
            }
            finally
            {
                var completedAfter = Interlocked.Increment(ref completed);
                var runningAfter = Interlocked.Decrement(ref running);
                var queuedAfter = total - runningAfter - completedAfter;
                CliRuntimeLog.Info("workflow", $"Progress update. total={total} queued={queuedAfter} running={runningAfter} completed={completedAfter} unit={index + 1}/{total} status={status}");
                limiter.Release();
            }
        }
    }

    private static int ResolveTranslationParallelism()
    {
        var raw = Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_TRANSLATION_PARALLELISM");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 8;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return 8;
        }

        return Math.Clamp(parsed, 1, 32);
    }

    private static int ResolveCuesPerGroup(int? overrideValue)
    {
        var parsed = overrideValue ?? ResolvePositiveIntFromEnvironment("SUBTITLEEXTRACTSLATOR_CUES_PER_GROUP", 10);
        return Math.Clamp(parsed, 1, 500);
    }

    private static int ResolveTranslationBodySize(int? overrideValue)
    {
        var parsed = overrideValue ?? ResolvePositiveIntFromEnvironment("SUBTITLEEXTRACTSLATOR_TRANSLATION_BODY_SIZE", 5);
        return Math.Clamp(parsed, 1, 32);
    }

    private static int ResolvePositiveIntFromEnvironment(string key, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        CliRuntimeLog.Warn("workflow", $"Invalid env {key}={raw}. Using fallback={fallback}.");
        return fallback;
    }

    private static string FormatGroupSymbols(IEnumerable<int> groupIndices)
    {
        var tokens = groupIndices
            .Distinct()
            .OrderBy(x => x)
            .Select(ToGroupToken)
            .ToList();

        return tokens.Count == 0 ? "-" : string.Concat(tokens);
    }

    private static string ToGroupToken(int groupIndex)
    {
        if (groupIndex <= 0)
        {
            return "?";
        }

        var n = groupIndex;
        var chars = new List<char>();
        while (n > 0)
        {
            n--;
            chars.Add((char)('a' + (n % 26)));
            n /= 26;
        }

        chars.Reverse();
        return new string(chars.ToArray());
    }

    private sealed record TranslationUnit(
        int MainGroupIndex,
        List<SubtitleGroup> BodyGroups,
        List<SubtitleGroup> ContextGroups);

    private static List<SubtitleGroup> BuildContextWindow(List<SubtitleGroup> groups, int index)
    {
        var result = new List<SubtitleGroup>();
        if (groups.Count == 0)
        {
            return result;
        }

        if (index > 0)
        {
            result.Add(groups[index - 1]);
        }

        result.Add(groups[index]);

        if (index + 1 < groups.Count)
        {
            result.Add(groups[index + 1]);
        }

        return result;
    }

    private static string BuildTemporaryExtractionPath(string output, string sourceLanguage)
    {
        var directory = Path.Combine(Path.GetTempPath(), "SubtitleExtractslator");
        Directory.CreateDirectory(directory);
        var baseName = Path.GetFileNameWithoutExtension(output);
        return Path.Combine(directory, $"{baseName}.{sourceLanguage}.{Guid.NewGuid():N}.tmp");
    }

    private static string NormalizeLanguageToken(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "und";
        }

        var token = language.Trim().ToLowerInvariant();
        return token.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_') ? token : "und";
    }

    private static bool AskUserYesNo(string prompt)
    {
        Console.Write(prompt);
        var text = Console.ReadLine();
        return string.Equals(text, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class SubtitleOperations
{
    public async Task<ProbeResult> ProbeTracksAsync(string input, string targetLanguage)
    {
        CliRuntimeLog.Info("probe", $"Start probe. input={input} target={targetLanguage}");
        if (Path.GetExtension(input).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            var inferred = InferLanguageFromFileName(input);
            var srtTracks = new List<SubtitleTrack> { new(0, 0, inferred, "subtitle-file") };
            var hasTargetFromSrt = inferred.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase);
            CliRuntimeLog.Info("probe", $"Input is SRT. inferredLanguage={inferred} hasTarget={hasTargetFromSrt}");
            return new ProbeResult(input, targetLanguage, hasTargetFromSrt, srtTracks);
        }

        var tracks = await ProbeWithFfprobeAsync(input);
        var hasTarget = tracks.Any(x => x.Language.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase));
        CliRuntimeLog.Info("probe", $"ffprobe tracks={tracks.Count} hasTarget={hasTarget}");
        return new ProbeResult(input, targetLanguage, hasTarget, tracks);
    }

    public Task<OpenSubtitlesResult> SearchOpenSubtitlesAsync(string input, string targetLanguage)
    {
        CliRuntimeLog.Info("opensubtitles", $"Searching candidates. input={input} target={targetLanguage}");
        var mock = Environment.GetEnvironmentVariable("OPENSUBTITLES_MOCK");
        if (string.IsNullOrWhiteSpace(mock))
        {
            CliRuntimeLog.Info("opensubtitles", "Mock is disabled (OPENSUBTITLES_MOCK empty). Returning empty candidate list.");
            return Task.FromResult(new OpenSubtitlesResult(input, targetLanguage, new List<SubtitleCandidate>()));
        }

        var candidates = new List<SubtitleCandidate>
        {
            new(1, targetLanguage, 0.92, $"{Path.GetFileNameWithoutExtension(input)}.{targetLanguage}.srt", "mock-source")
        };

        CliRuntimeLog.Info("opensubtitles", $"Mock candidate generated. count={candidates.Count}");

        return Task.FromResult(new OpenSubtitlesResult(input, targetLanguage, candidates));
    }

    public async Task<ExtractionResult> ExtractSubtitleAsync(string input, string output, string preferredLanguage)
    {
        CliRuntimeLog.Info("extract", $"Start extract. input={input} output={output} preferred={preferredLanguage}");
        if (Path.GetExtension(input).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(input, output, overwrite: true);
            var inferred = InferLanguageFromFileName(input);
            CliRuntimeLog.Info("extract", "Input is SRT. Copy completed.");
            return new ExtractionResult(input, output, inferred, "copied-input-srt");
        }

        var tracks = await ProbeWithFfprobeAsync(input);
        var selected = SelectBestTrack(tracks, preferredLanguage);

        if (selected is null)
        {
            throw new InvalidOperationException("No subtitle track found in source media.");
        }

        var ffmpeg = ResolveExecutable("ffmpeg");
        var args = $"-y -i \"{input}\" -map 0:s:{selected.SubtitleOrder} -f srt \"{output}\"";
        CliRuntimeLog.Info("extract", $"Selected subtitle track order={selected.SubtitleOrder} language={selected.Language}");
        CliRuntimeLog.Info("extract", $"Running ffmpeg: {ffmpeg} {args}");
        await RunProcessAsync(ffmpeg, args);
        CliRuntimeLog.Info("extract", "ffmpeg extraction completed.");

        return new ExtractionResult(input, output, selected.Language, "ffmpeg-extract");
    }

    public async Task<List<SubtitleCue>> LoadSrtAsync(string path)
    {
        CliRuntimeLog.Info("srt", $"Loading SRT: {path}");
        var content = await File.ReadAllTextAsync(path, Encoding.UTF8);
        var parsed = SrtSerializer.Parse(content);
        CliRuntimeLog.Info("srt", $"SRT loaded. chars={content.Length} cues={parsed.Count}");
        return parsed;
    }

    public async Task SaveSrtAsync(string path, List<SubtitleCue> cues)
    {
        CliRuntimeLog.Info("srt", $"Saving SRT: {path} cues={cues.Count}");
        var text = SrtSerializer.Serialize(cues);
        await File.WriteAllTextAsync(path, text, Encoding.UTF8);
        CliRuntimeLog.Info("srt", $"SRT saved. chars={text.Length}");
    }

    private static SubtitleTrack? SelectBestTrack(List<SubtitleTrack> tracks, string preferredLanguage)
    {
        var exact = tracks.FirstOrDefault(x => x.Language.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var english = tracks.FirstOrDefault(x => x.Language.Equals("en", StringComparison.OrdinalIgnoreCase)
            || x.Language.Equals("eng", StringComparison.OrdinalIgnoreCase));
        if (english is not null)
        {
            return english;
        }

        return tracks.FirstOrDefault();
    }

    private static async Task<List<SubtitleTrack>> ProbeWithFfprobeAsync(string input)
    {
        using var scope = CliRuntimeLog.BeginScope("ffprobe", $"Probe subtitle streams from media: {input}");
        await FfmpegBootstrap.EnsureAsync();

        var ffprobe = ResolveExecutable("ffprobe");
        var args = $"-v error -select_streams s -show_entries stream=index:stream_tags=language,title -of json \"{input}\"";
        CliRuntimeLog.Info("ffprobe", $"Running ffprobe: {ffprobe} {args}");
        var output = await RunProcessAsync(ffprobe, args);

        using var doc = JsonDocument.Parse(output);
        if (!doc.RootElement.TryGetProperty("streams", out var streams))
        {
            return new List<SubtitleTrack>();
        }

        var tracks = new List<SubtitleTrack>();
        var i = 0;
        foreach (var item in streams.EnumerateArray())
        {
            var index = item.TryGetProperty("index", out var idxEl) ? idxEl.GetInt32() : i;
            var language = "und";
            var title = "";

            if (item.TryGetProperty("tags", out var tags))
            {
                if (tags.TryGetProperty("language", out var langEl))
                {
                    language = langEl.GetString() ?? "und";
                }

                if (tags.TryGetProperty("title", out var titleEl))
                {
                    title = titleEl.GetString() ?? "";
                }
            }

            tracks.Add(new SubtitleTrack(index, i, language, title));
            i++;
        }

        CliRuntimeLog.Info("ffprobe", $"ffprobe parse completed. streamCount={tracks.Count}");

        return tracks;
    }

    private static string InferLanguageFromFileName(string input)
    {
        var name = Path.GetFileNameWithoutExtension(input);
        var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            var token = parts[^1];
            if (token.Length is 2 or 3)
            {
                return token.ToLowerInvariant();
            }
        }

        return "und";
    }

    private static string ResolveExecutable(string name)
    {
        var configuredBin = FfmpegBootstrap.GetConfiguredBinPath();
        var ext = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        var full = name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name : name + ext;

        if (!string.IsNullOrWhiteSpace(configuredBin))
        {
            var candidate = Path.Combine(configuredBin, full);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return full;
    }

    private static async Task<string> RunProcessAsync(string fileName, string args)
    {
        using var scope = CliRuntimeLog.BeginScope("process", $"Execute process: {fileName}");
        var psi = new ProcessStartInfo(fileName, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2 || ex.NativeErrorCode == 3)
        {
            throw new InvalidOperationException(BuildMissingDependencyMessage(fileName), ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            CliRuntimeLog.Error("process", $"Process failed. exitCode={process.ExitCode}");
            throw new InvalidOperationException($"Process failed ({fileName} {args})\n{stderr}");
        }

        CliRuntimeLog.Info("process", $"Process succeeded. stdoutLen={stdout.Length} stderrLen={stderr.Length}");

        return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
    }

    private static string BuildMissingDependencyMessage(string executable)
    {
        var dependency = executable.EndsWith("ffprobe.exe", StringComparison.OrdinalIgnoreCase)
            || executable.Equals("ffprobe", StringComparison.OrdinalIgnoreCase)
            ? "ffprobe"
            : "ffmpeg";

        string installHint;
        if (OperatingSystem.IsWindows())
        {
            installHint = "Windows: install a current FFmpeg build, e.g. `winget install Gyan.FFmpeg` (or `choco install ffmpeg`).";
        }
        else if (OperatingSystem.IsMacOS())
        {
            installHint = "macOS: install a current FFmpeg build, e.g. `brew install ffmpeg`.";
        }
        else
        {
            installHint = "Linux: install a current FFmpeg build, e.g. `sudo apt install ffmpeg` (or your distro equivalent).";
        }

        return $"Missing required dependency: {dependency}. The app attempts NuGet-based FFmpeg bootstrap first, then PATH fallback. Set FFMPEG_BIN_DIR to a folder containing ffmpeg/ffprobe, or install FFmpeg and ensure `{dependency}` is available in PATH. {installHint}";
    }
}

internal static class FfmpegBootstrap
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _initialized;

    public static async Task EnsureAsync()
    {
        if (_initialized)
        {
            return;
        }

        await Gate.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            var overrideDir = Environment.GetEnvironmentVariable("FFMPEG_BIN_DIR");
            if (!string.IsNullOrWhiteSpace(overrideDir) && HasExecutables(overrideDir))
            {
                LogInfo($"Using FFmpeg from FFMPEG_BIN_DIR: {overrideDir}");
                FFmpeg.SetExecutablesPath(overrideDir);
                _initialized = true;
                return;
            }

            var knownBinDir = FindKnownSystemBinDir();
            if (!string.IsNullOrWhiteSpace(knownBinDir))
            {
                LogInfo($"Using FFmpeg from known system location: {knownBinDir}");
                FFmpeg.SetExecutablesPath(knownBinDir);
                _initialized = true;
                return;
            }

            var downloadDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SubtitleExtractslator",
                "ffmpeg");

            if (!HasExecutables(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
                LogInfo($"FFmpeg not found locally. Downloading to: {downloadDir}");
                try
                {
                    var lastLoggedPercent = -10;
                    var progress = new Progress<ProgressInfo>(p =>
                    {
                        if (p.TotalBytes <= 0)
                        {
                            return;
                        }

                        var percent = (int)Math.Round(p.DownloadedBytes * 100d / p.TotalBytes, MidpointRounding.AwayFromZero);
                        if (percent >= lastLoggedPercent + 10 || percent == 100)
                        {
                            lastLoggedPercent = percent;
                            LogInfo($"FFmpeg download progress: {percent}% ({p.DownloadedBytes}/{p.TotalBytes} bytes)");
                        }
                    });

                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, downloadDir, progress);
                    LogInfo("FFmpeg download completed.");
                }
                catch (Exception ex)
                {
                    LogWarn($"FFmpeg download failed, falling back to PATH lookup. Reason: {ex.Message}");
                }
            }
            else
            {
                LogInfo($"Found existing FFmpeg binaries at: {downloadDir}");
            }

            if (HasExecutables(downloadDir))
            {
                LogInfo($"Using FFmpeg binaries from: {downloadDir}");
                FFmpeg.SetExecutablesPath(downloadDir);
            }

            _initialized = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static string? GetConfiguredBinPath()
    {
        var overrideDir = Environment.GetEnvironmentVariable("FFMPEG_BIN_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDir) && HasExecutables(overrideDir))
        {
            return overrideDir;
        }

        var knownBinDir = FindKnownSystemBinDir();
        if (!string.IsNullOrWhiteSpace(knownBinDir))
        {
            return knownBinDir;
        }

        return string.IsNullOrWhiteSpace(FFmpeg.ExecutablesPath) ? null : FFmpeg.ExecutablesPath;
    }

    private static bool HasExecutables(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var ffmpegName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var ffprobeName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

        return File.Exists(Path.Combine(directory, ffmpegName))
            && File.Exists(Path.Combine(directory, ffprobeName));
    }

    private static string? FindKnownSystemBinDir()
    {
        IEnumerable<string> candidates;

        if (OperatingSystem.IsWindows())
        {
            var win = new List<string>();
            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                win.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Links"));
            }

            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                win.Add(Path.Combine(userProfile, "AppData", "Local", "Microsoft", "WinGet", "Links"));
            }

            candidates = win;
        }
        else if (OperatingSystem.IsMacOS())
        {
            candidates =
            [
                "/opt/homebrew/bin",
                "/usr/local/bin",
                "/opt/local/bin",
                "/usr/bin"
            ];
        }
        else
        {
            candidates =
            [
                "/usr/local/bin",
                "/usr/bin",
                "/snap/bin",
                "/home/linuxbrew/.linuxbrew/bin"
            ];
        }

        return candidates.FirstOrDefault(HasExecutables);
    }

    private static void LogInfo(string message)
    {
        Console.Error.WriteLine($"[ffmpeg-bootstrap] {message}");
    }

    private static void LogWarn(string message)
    {
        Console.Error.WriteLine($"[ffmpeg-bootstrap][warn] {message}");
    }
}

internal static class GroupingEngine
{
    public static List<SubtitleGroup> Group(List<SubtitleCue> cues, int cuesPerGroup = 10)
    {
        var ordered = cues.OrderBy(x => x.Start).ToList();
        var groups = new List<SubtitleGroup>();
        cuesPerGroup = Math.Max(1, cuesPerGroup);
        for (var i = 0; i < ordered.Count; i += cuesPerGroup)
        {
            var take = Math.Min(cuesPerGroup, ordered.Count - i);
            groups.Add(new SubtitleGroup(0, ordered.GetRange(i, take)));
        }

        return ReindexGroups(groups);
    }

    private static List<SubtitleGroup> ReindexGroups(List<SubtitleGroup> groups)
    {
        var output = new List<SubtitleGroup>(groups.Count);
        for (var i = 0; i < groups.Count; i++)
        {
            output.Add(new SubtitleGroup(i + 1, groups[i].Cues));
        }

        return output;
    }
}

internal static class RollingKnowledge
{
    public static string BuildSceneSummary(SubtitleGroup group, string historical)
    {
        CliRuntimeLog.Info("context", $"Build scene summary for group={group.GroupIndex} cues={group.Cues.Count}");
        var lines = group.Cues.SelectMany(x => x.Lines).Take(6);
        var summary = string.Join(" ", lines);
        if (summary.Length > 180)
        {
            summary = summary[..180];
        }

        return $"Group {group.GroupIndex} scene: {summary}. Historical: {historical}";
    }

    public static string UpdateHistoricalKnowledge(string historical, string summary, SubtitleGroup group)
    {
        return $"v{group.GroupIndex + 1}: {summary}";
    }
}

internal sealed class TranslationPipeline
{
    private readonly ITranslationProvider _externalProvider;
    private readonly ITranslationProvider _samplingProvider;

    public TranslationPipeline(ModeContext modeContext, ITranslationProvider externalProvider, ITranslationProvider samplingProvider)
    {
        ModeContext = modeContext;
        _externalProvider = externalProvider;
        _samplingProvider = samplingProvider;
    }

    public ModeContext ModeContext { get; }

    public async Task<GroupTranslationResult> TranslateGroupAsync(
        SubtitleGroup group,
        IReadOnlyList<SubtitleGroup> contextWindow,
        string targetLanguage)
    {
        using var scope = CliRuntimeLog.BeginScope("translate", $"Translate group {group.GroupIndex}");
        var contextCueTexts = contextWindow
            .SelectMany(x => x.Cues)
            .Select(x => FlattenCueLines(x.Lines))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        var contextHint = BuildContextHint(group.GroupIndex, contextWindow);
        CliRuntimeLog.Info("translate", $"Context cues={contextCueTexts.Count} window={contextHint}");

        var sourceCueTexts = group.Cues.Select(x => FlattenCueLines(x.Lines)).ToList();
        CliRuntimeLog.Info("translate", $"Main group cues={sourceCueTexts.Count} target={targetLanguage}");

        var contextGuide = BuildSinglePassContextGuide(group, contextWindow);
        CliRuntimeLog.Info("translate", $"Single-pass context guide chars={contextGuide.Length}");

        IReadOnlyList<string> translated;
        if (ModeContext == ModeContext.Mcp)
        {
            CliRuntimeLog.Info("translate", "Mode=MCP. Try sampling provider first.");
            translated = await _samplingProvider.TranslateIndexedAsync(sourceCueTexts, targetLanguage, contextGuide, contextHint);
            if (translated.Count == 0)
            {
                CliRuntimeLog.Warn("translate", "Sampling provider returned empty result. Fallback to external provider.");
                translated = await _externalProvider.TranslateIndexedAsync(sourceCueTexts, targetLanguage, contextGuide, contextHint);
            }
        }
        else
        {
            CliRuntimeLog.Info("translate", "Mode=CLI. Use external provider directly.");
            translated = await _externalProvider.TranslateIndexedAsync(sourceCueTexts, targetLanguage, contextGuide, contextHint);
        }

        var rebuilt = RebuildCues(group.Cues, translated);
        ValidateStructure(group.Cues, rebuilt);
        CliRuntimeLog.Info("translate", $"Group {group.GroupIndex} validation passed. translatedCues={translated.Count}");
        return new GroupTranslationResult(group.GroupIndex, contextGuide, contextHint, rebuilt);
    }

    private static string BuildContextHint(int mainGroupIndex, IReadOnlyList<SubtitleGroup> contextWindow)
    {
        var ids = string.Join(',', contextWindow.Select(x => x.GroupIndex));
        return $"main={mainGroupIndex}; window=[{ids}]";
    }

    private static string BuildSinglePassContextGuide(SubtitleGroup mainGroup, IReadOnlyList<SubtitleGroup> contextWindow)
    {
        var mainCueIds = new HashSet<int>(mainGroup.Cues.Select(x => x.Index));
        var before = contextWindow
            .SelectMany(x => x.Cues)
            .Where(c => c.Index < mainGroup.Cues.Min(mc => mc.Index))
            .OrderBy(c => c.Index)
            .Select(c => FlattenCueLines(c.Lines))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        var main = mainGroup.Cues
            .OrderBy(c => c.Index)
            .Select(c => FlattenCueLines(c.Lines))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        var after = contextWindow
            .SelectMany(x => x.Cues)
            .Where(c => c.Index > mainGroup.Cues.Max(mc => mc.Index) && !mainCueIds.Contains(c.Index))
            .OrderBy(c => c.Index)
            .Select(c => FlattenCueLines(c.Lines))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<translation_context>");
        sb.AppendLine("  <instructions>");
        sb.AppendLine("    Read all sections for context. Translate only content in <main_section>.");
        sb.AppendLine("  </instructions>");
        sb.AppendLine("  <previous_context>");
        AppendPlainLines(sb, before, "    ");
        sb.AppendLine("  </previous_context>");
        sb.AppendLine("  <main_section>");  
        AppendPlainLines(sb, main, "    "); 
        sb.AppendLine("  </main_section>");
        sb.AppendLine("  <following_context>");
        AppendPlainLines(sb, after, "    ");
        sb.AppendLine("  </following_context>");
        sb.AppendLine("</translation_context>");
        return sb.ToString().Trim();
    }

    private static void AppendPlainLines(StringBuilder sb, IReadOnlyList<string> lines, string indent = "")
    {
        if (lines.Count == 0)
        {
            sb.AppendLine($"{indent}(none)");
            return;
        }

        foreach (var line in lines)
        {
            sb.AppendLine($"{indent}{line}");
        }
    }

    private static List<SubtitleCue> RebuildCues(List<SubtitleCue> original, IReadOnlyList<string> translatedCueTexts)
    {
        var rebuilt = new List<SubtitleCue>();

        for (var i = 0; i < original.Count; i++)
        {
            var cue = original[i];
            var translatedText = i < translatedCueTexts.Count
                ? translatedCueTexts[i]
                : FlattenCueLines(cue.Lines);
            var lines = WrapCueTextByDisplayWidth(translatedText, 32);
            rebuilt.Add(cue with { Lines = lines });
        }

        return rebuilt;
    }

    private static string FlattenCueLines(List<string> lines)
    {
        return string.Join(" ", lines.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
    }

    private static List<string> WrapCueTextByDisplayWidth(string text, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return new List<string> { text };
        }

        var normalized = Regex.Replace(text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal), "\\s+", " ").Trim();
        if (normalized.Length == 0)
        {
            return new List<string> { string.Empty };
        }

        if (normalized.Contains(' '))
        {
            return WrapByWords(normalized, maxWidth);
        }

        return WrapByChars(normalized, maxWidth);
    }

    private static List<string> WrapByWords(string text, int maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();
        var width = 0;

        foreach (var word in words)
        {
            var wordWidth = ComputeDisplayWidth(word);
            var extra = current.Length == 0 ? 0 : 1;
            if (current.Length > 0 && width + extra + wordWidth > maxWidth)
            {
                lines.Add(current.ToString());
                current.Clear();
                width = 0;
            }

            if (current.Length > 0)
            {
                current.Append(' ');
                width += 1;
            }

            if (wordWidth > maxWidth)
            {
                foreach (var chunk in WrapByChars(word, maxWidth))
                {
                    if (current.Length > 0)
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                        width = 0;
                    }

                    lines.Add(chunk);
                }

                continue;
            }

            current.Append(word);
            width += wordWidth;
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines.Count == 0 ? new List<string> { string.Empty } : lines;
    }

    private static List<string> WrapByChars(string text, int maxWidth)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        var width = 0;

        foreach (var ch in text)
        {
            var cw = CharDisplayWidth(ch);
            if (current.Length > 0 && width + cw > maxWidth)
            {
                lines.Add(current.ToString());
                current.Clear();
                width = 0;
            }

            current.Append(ch);
            width += cw;
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines.Count == 0 ? new List<string> { string.Empty } : lines;
    }

    private static int ComputeDisplayWidth(string text)
    {
        var sum = 0;
        foreach (var c in text)
        {
            sum += CharDisplayWidth(c);
        }

        return sum;
    }

    private static int CharDisplayWidth(char c)
    {
        return IsWideChar(c) ? 2 : 1;
    }

    private static bool IsWideChar(char c)
    {
        return (c >= '\u1100' && c <= '\u115F')
            || (c >= '\u2E80' && c <= '\uA4CF')
            || (c >= '\uAC00' && c <= '\uD7A3')
            || (c >= '\uF900' && c <= '\uFAFF')
            || (c >= '\uFE10' && c <= '\uFE19')
            || (c >= '\uFE30' && c <= '\uFE6F')
            || (c >= '\uFF01' && c <= '\uFF60')
            || (c >= '\uFFE0' && c <= '\uFFE6');
    }

    private static void ValidateStructure(List<SubtitleCue> original, List<SubtitleCue> translated)
    {
        if (original.Count != translated.Count)
        {
            throw new InvalidOperationException("Translation changed cue count.");
        }

        for (var i = 0; i < original.Count; i++)
        {
            if (original[i].Index != translated[i].Index || original[i].Start != translated[i].Start || original[i].End != translated[i].End)
            {
                throw new InvalidOperationException($"Translation changed timeline at cue index {original[i].Index}.");
            }

            if (translated[i].Lines.Count == 0)
            {
                throw new InvalidOperationException($"Translation produced empty lines at cue index {original[i].Index}.");
            }
        }
    }
}

internal interface ITranslationProvider
{
    Task<IReadOnlyList<string>> TranslateIndexedAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string contextParaphrase,
        string contextHint);

    Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string paraphraseSummary,
        string previousCycleParaphrase,
        string paraphraseHistory);
}

internal sealed class SamplingTranslationProvider : ITranslationProvider
{
    public Task<IReadOnlyList<string>> TranslateIndexedAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string contextParaphrase,
        string contextHint)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string paraphraseSummary,
        string previousCycleParaphrase,
        string paraphraseHistory)
    {
        return TranslateIndexedAsync(lines, targetLanguage, paraphraseSummary, paraphraseHistory);
    }
}

internal sealed class ExternalTranslationProvider : ITranslationProvider
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromHours(1)
    };
    private static readonly TokenCredential AzureCredential = new DefaultAzureCredential();

    public async Task<IReadOnlyList<string>> TranslateIndexedAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string contextParaphrase,
        string contextHint)
    {
        using var scope = CliRuntimeLog.BeginScope("llm", $"Start external translation call. lines={lines.Count} target={targetLanguage}");
        if (lines.Count == 0)
        {
            CliRuntimeLog.Info("llm", "No lines to translate. Returning empty result.");
            return Array.Empty<string>();
        }

        var settings = LlmSettings.FromEnvironment();
        CliRuntimeLog.Info("llm", $"Resolved settings. apiType={settings.ApiType} endpoint={settings.Endpoint} model={settings.Model} retryCount={settings.RetryCount}");
        var systemPrompt = settings.SystemPrompt
            ?? "You are a subtitle translator. Use the provided context sections to reason first, then output only the line-by-line translation for the target lines. Never add commentary in final output. Keep every index and return exactly one translated line for each input index.";

        var indexedInput = BuildIndexedInput(lines, targetLanguage, contextParaphrase, contextHint);
        CliRuntimeLog.Info("llm", $"Prompt built. chars={indexedInput.Length}");
        var maxAttempts = settings.RetryCount;
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            CliRuntimeLog.Info("llm", $"LLM request attempt {attempt}/{maxAttempts}.");
            try
            {
                var execution = await ExecutePromptAsync(settings, systemPrompt, indexedInput);
                var output = execution.OutputText;
                CliRuntimeLog.Info("llm", $"Output text extracted. chars={output?.Length ?? 0} outputTokens={execution.OutputTokenCount?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}");

                var ioDumpPath = ErrorSnapshotWriter.WriteMarkdown(
                    "llm-io",
                    new Dictionary<string, string?>
                    {
                        ["attempt"] = $"{attempt}/{maxAttempts}",
                        ["endpoint"] = settings.Endpoint,
                        ["model"] = settings.Model,
                        ["apiType"] = settings.ApiType,
                        ["targetLanguage"] = targetLanguage,
                        ["expectedLineCount"] = lines.Count.ToString(CultureInfo.InvariantCulture),
                        ["contextHint"] = contextHint,
                        ["reasoningSetting"] = settings.Reasoning ?? "low",
                        ["reasoningOutput"] = execution.ReasoningText,
                        ["outputTokens"] = execution.OutputTokenCount?.ToString(CultureInfo.InvariantCulture) ?? "n/a",
                        ["systemPrompt"] = systemPrompt,
                        ["inputPrompt"] = indexedInput,
                        ["output"] = output
                    },
                    BuildSnapshotTag(contextHint, attempt, maxAttempts));
                CliRuntimeLog.Info("llm", $"LLM I/O dump written: {ioDumpPath}");

     

                if (string.IsNullOrWhiteSpace(output))
                {
                    var snapshotPath = ErrorSnapshotWriter.Write(
                        "llm-empty-output",
                        new Dictionary<string, string?>
                        {
                            ["endpoint"] = settings.Endpoint,
                            ["model"] = settings.Model,
                            ["apiType"] = settings.ApiType,
                            ["targetLanguage"] = targetLanguage,
                            ["expectedLineCount"] = lines.Count.ToString(CultureInfo.InvariantCulture),
                            ["attempt"] = attempt.ToString(CultureInfo.InvariantCulture),
                            ["systemPrompt"] = systemPrompt,
                            ["inputPrompt"] = indexedInput,
                            ["extractedOutput"] = output
                        });
                    throw new InvalidOperationException(
                        "LLM translation response did not contain usable output text.\n"
                        + $"Detailed LLM log: {snapshotPath}");
                }

                var translated = ParseIndexedOutput(output, lines.Count);
                CliRuntimeLog.Info("llm", $"Indexed output parsed. parsedLines={translated.Count} expectedLines={lines.Count}");
                if (translated.Count != lines.Count)
                {
                    var snapshotPath = ErrorSnapshotWriter.Write(
                        "llm-linecount-mismatch",
                        new Dictionary<string, string?>
                        {
                            ["endpoint"] = settings.Endpoint,
                            ["model"] = settings.Model,
                            ["apiType"] = settings.ApiType,
                            ["targetLanguage"] = targetLanguage,
                            ["expectedLineCount"] = lines.Count.ToString(CultureInfo.InvariantCulture),
                            ["parsedLineCount"] = translated.Count.ToString(CultureInfo.InvariantCulture),
                            ["attempt"] = attempt.ToString(CultureInfo.InvariantCulture),
                            ["systemPrompt"] = systemPrompt,
                            ["inputPrompt"] = indexedInput,
                            ["extractedOutput"] = output
                        });
                    throw new InvalidOperationException(
                        $"LLM returned invalid line count. Expected {lines.Count}, received {translated.Count}.\n"
                        + $"Detailed LLM log: {snapshotPath}");
                }

                return translated;
            }
            catch (Exception ex)
            {
                var reasoningFromError = ex is LlmRequestException reqEx ? reqEx.ReasoningText : null;
                var responseBodyFromError = ex is LlmRequestException reqEx2 ? reqEx2.ResponseBody : null;
                var ioDumpPath = ErrorSnapshotWriter.WriteMarkdown(
                    "llm-io-error",
                    new Dictionary<string, string?>
                    {
                        ["attempt"] = $"{attempt}/{maxAttempts}",
                        ["endpoint"] = settings.Endpoint,
                        ["model"] = settings.Model,
                        ["apiType"] = settings.ApiType,
                        ["targetLanguage"] = targetLanguage,
                        ["expectedLineCount"] = lines.Count.ToString(CultureInfo.InvariantCulture),
                        ["contextHint"] = contextHint,
                        ["reasoningSetting"] = settings.Reasoning ?? "low",
                        ["reasoningOutput"] = reasoningFromError,
                        ["responseBody"] = responseBodyFromError,
                        ["systemPrompt"] = systemPrompt,
                        ["inputPrompt"] = indexedInput,
                        ["error"] = ex.ToString()
                    },
                    BuildSnapshotTag(contextHint, attempt, maxAttempts));
                CliRuntimeLog.Warn("llm", $"LLM I/O error dump written: {ioDumpPath}");

                lastError = ex;
                CliRuntimeLog.Warn("llm", $"Attempt {attempt}/{maxAttempts} failed: {ex.Message}");
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt));
                    continue;
                }
            }
        }

        throw new InvalidOperationException($"LLM translation failed after {maxAttempts} attempts.", lastError);
    }

    private static string BuildSnapshotTag(string contextHint, int attempt, int maxAttempts)
    {
        var main = "main-unknown";
        var marker = "main=";
        var start = contextHint.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start >= 0)
        {
            start += marker.Length;
            var end = contextHint.IndexOf(';', start);
            var token = end >= 0 ? contextHint[start..end] : contextHint[start..];
            if (!string.IsNullOrWhiteSpace(token))
            {
                main = $"main-{token.Trim()}";
            }
        }

        return $"{main}.attempt-{attempt}-of-{maxAttempts}";
    }

    public Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string paraphraseSummary,
        string previousCycleParaphrase,
        string paraphraseHistory)
    {
        var mergedParaphrase = string.Join("\n", new[] { paraphraseSummary, previousCycleParaphrase, paraphraseHistory }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return TranslateIndexedAsync(lines, targetLanguage, mergedParaphrase, "legacy");
    }

    private async Task<LlmExecutionResult> ExecutePromptAsync(LlmSettings settings, string systemPrompt, string userPrompt)
    {
        var requestPayload = BuildRequestPayload(settings, systemPrompt, userPrompt);
        string? body = null;
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
        {
            Content = JsonContent.Create(requestPayload)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        CliRuntimeLog.Info("llm", "Applying authorization headers.");
        await ApplyAuthorizationAsync(request, settings);

        CliRuntimeLog.Info("llm", "Sending HTTP request to translation endpoint.");
        using var response = await Http.SendAsync(request);
        body = await response.Content.ReadAsStringAsync();
        CliRuntimeLog.Info("llm", $"Received response. status={(int)response.StatusCode} bodyChars={body.Length}");
        var reasoningText = TryExtractReasoningText(body);
        if (!response.IsSuccessStatusCode)
        {
            var snapshotPath = ErrorSnapshotWriter.Write(
                "llm-http-error",
                new Dictionary<string, string?>
                {
                    ["endpoint"] = settings.Endpoint,
                    ["model"] = settings.Model,
                    ["apiType"] = settings.ApiType,
                    ["systemPrompt"] = systemPrompt,
                    ["inputPrompt"] = userPrompt,
                    ["responseStatus"] = $"{(int)response.StatusCode} {response.ReasonPhrase}",
                    ["responseBody"] = body
                });
            throw new LlmRequestException(
                $"LLM translation request failed ({(int)response.StatusCode} {response.ReasonPhrase}). Endpoint: {settings.Endpoint}. Body: {body}\n"
                + $"Detailed LLM log: {snapshotPath}",
                body,
                reasoningText);
        }

        var outputText = TryExtractOutputText(body, settings.ApiType) ?? string.Empty;
        var outputTokenCount = TryExtractOutputTokenCount(body, settings.ApiType)
            ?? ApproximateTokenCount(outputText);
        return new LlmExecutionResult(outputText, outputTokenCount, reasoningText);
    }

    private static string BuildIndexedInput(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string contextParaphrase,
        string contextHint)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Translate the following MAIN GROUP subtitle cues to {targetLanguage}.");
        sb.AppendLine("STRICT FORMAT RULES:");
        sb.AppendLine("1) Keep numbering unchanged and contiguous. Do not skip any index.");
        sb.AppendLine("2) Keep one output line for each input cue. Total output lines must equal total input lines.");
        sb.AppendLine("3) Never delete non-dialogue lines, including CC/music/sound/environment descriptions.");
        sb.AppendLine("4) Preserve conversational atmosphere, character voice, and humor. Keep jokes and references where possible; do not dilute punchlines.");
        sb.AppendLine("5) Think silently in this order before output: (a) first create a brief holistic paraphrase of the whole MAIN GROUP in your mind, (b) then refine line-by-line details and references, (c) preserve jokes/puns/religion/sexual/pop-culture effects, (d) align each output line to its input index.");
        sb.AppendLine("6) Context is provided in XML sections. Use <previous_context> and <following_context> only for guidance. Translate only the indexed MAIN GROUP lines that correspond to <main_section>.");
        sb.AppendLine("Return ONLY numbered lines in the exact format: [index]\ttranslated text");
        sb.AppendLine($"Context hint: {contextHint}");
        sb.AppendLine("Context sections:");
        sb.AppendLine(contextParaphrase);
        sb.AppendLine("Main group lines:");

        for (var i = 0; i < lines.Count; i++)
        {
            sb.AppendLine($"[{i + 1}]\t{lines[i]}");
        }

        return sb.ToString();
    }

    private static object BuildRequestPayload(LlmSettings settings, string systemPrompt, string indexedInput)
    {
        var endpointIsLegacyLocalChat = settings.Endpoint.EndsWith("/api/v1/chat", StringComparison.OrdinalIgnoreCase);
        if (settings.ApiType == "openai" && endpointIsLegacyLocalChat)
        {
            // Keep compatibility with local chat API shape from default LM Studio style config.
            var payload = new Dictionary<string, object?>
            {
                ["model"] = settings.Model,
                ["system_prompt"] = systemPrompt,
                ["input"] = indexedInput
            };

            if (!string.IsNullOrWhiteSpace(settings.Reasoning)
                && !settings.Reasoning.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                payload["reasoning"] = settings.Reasoning;
            }

            return payload;
        }

        return settings.ApiType switch
        {
            "claude" => new
            {
                model = settings.Model,
                system = systemPrompt,
                max_tokens = settings.MaxTokens,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = indexedInput
                    }
                }
            },
            _ => BuildOpenAiPayload(settings, systemPrompt, indexedInput)
        };
    }

    private static string? NormalizeReasoningForRequest(string? reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning)
            || reasoning.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return reasoning;
    }

    private static Dictionary<string, object?> BuildOpenAiPayload(
        LlmSettings settings,
        string systemPrompt,
        string indexedInput)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = indexedInput }
            },
            ["temperature"] = 0.2
        };

        var reasoning = NormalizeReasoningForRequest(settings.Reasoning);
        if (!string.IsNullOrWhiteSpace(reasoning))
        {
            payload["reasoning"] = reasoning;
        }

        return payload;
    }

    private static int? TryExtractOutputTokenCount(string json, string apiType)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in apiType == "claude"
                         ? new[] { "output_tokens", "completion_tokens", "total_tokens" }
                         : new[] { "completion_tokens", "output_tokens", "total_tokens" })
            {
                if (usage.TryGetProperty(key, out var tokenEl)
                    && tokenEl.ValueKind == JsonValueKind.Number
                    && tokenEl.TryGetInt32(out var parsed)
                    && parsed >= 0)
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static int ApproximateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        // Coarse fallback for providers that do not return usage token fields.
        return Math.Max(1, text.Length / 4);
    }

    private static string? TryExtractReasoningText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var buffer = new List<string>();
        CollectReasoningSegments(doc.RootElement, buffer);
        return buffer.Count == 0 ? null : string.Join("\n", buffer);
    }

    private static void CollectReasoningSegments(JsonElement element, List<string> buffer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                if (element.TryGetProperty("type", out var typeEl)
                    && typeEl.ValueKind == JsonValueKind.String)
                {
                    var type = typeEl.GetString();
                    if (type is not null
                        && (type.Equals("reasoning", StringComparison.OrdinalIgnoreCase)
                            || type.Equals("thinking", StringComparison.OrdinalIgnoreCase)
                            || type.Equals("thought", StringComparison.OrdinalIgnoreCase)))
                    {
                        var text = ExtractTextFromElement(element, skipReasoning: false);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            buffer.Add(text.Trim());
                        }
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    CollectReasoningSegments(property.Value, buffer);
                }

                break;
            }

            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    CollectReasoningSegments(item, buffer);
                }

                break;
            }
        }
    }

    private sealed record LlmExecutionResult(string OutputText, int? OutputTokenCount, string? ReasoningText);

    private sealed class LlmRequestException : InvalidOperationException
    {
        public LlmRequestException(string message, string? responseBody, string? reasoningText)
            : base(message)
        {
            ResponseBody = responseBody;
            ReasoningText = reasoningText;
        }

        public string? ResponseBody { get; }

        public string? ReasoningText { get; }
    }

    private static async Task ApplyAuthorizationAsync(HttpRequestMessage request, LlmSettings settings)
    {
        switch (settings.AuthType)
        {
            case "none":
                return;
            case "azure-rbac":
            {
                var scope = Environment.GetEnvironmentVariable("LLM_AZURE_SCOPE") ?? "https://cognitiveservices.azure.com/.default";
                var token = await AzureCredential.GetTokenAsync(new TokenRequestContext(new[] { scope }), CancellationToken.None);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                return;
            }
            default:
            {
                if (string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    throw new InvalidOperationException(
                        "LLM auth type is 'key' but no key is configured. Set LLM_API_KEY (or OPENAI_API_KEY/ANTHROPIC_API_KEY).");
                }

                if (settings.ApiType == "claude")
                {
                    request.Headers.TryAddWithoutValidation("x-api-key", settings.ApiKey);
                    request.Headers.TryAddWithoutValidation(
                        "anthropic-version",
                        Environment.GetEnvironmentVariable("LLM_ANTHROPIC_VERSION") ?? "2023-06-01");
                    return;
                }

                if (settings.Endpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.TryAddWithoutValidation("api-key", settings.ApiKey);
                }
                else
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                }

                return;
            }
        }
    }

    private static string? TryExtractOutputText(string json, string apiType)
    {
        string? extracted;
        if (apiType == "claude")
        {
            extracted = TryExtractClaudeOutputText(json);
        }
        else
        {
            extracted = TryExtractOpenAiStyleOutputText(json);
        }

        return NormalizeModelOutputText(extracted);
    }

    private static bool ShouldDumpPromptForDebug()
    {
        var value = Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_DUMP_PROMPT");
        return string.Equals(value, "1", StringComparison.Ordinal)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeModelOutputText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        const string thinkCloseTag = "</think>";
        var marker = text.LastIndexOf(thinkCloseTag, StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
        {
            return text[(marker + thinkCloseTag.Length)..].TrimStart('\r', '\n', '\t', ' ');
        }

        return text;
    }

    private static string? TryExtractOpenAiStyleOutputText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("output_text", out var outputText))
        {
            var value = ExtractTextFromElement(outputText, skipReasoning: true);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.String)
        {
            return output.GetString();
        }

        if (root.TryGetProperty("output", out output))
        {
            var value = ExtractTextFromElement(output, skipReasoning: true);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (root.TryGetProperty("content", out content))
        {
            var value = ExtractTextFromElement(content, skipReasoning: true);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (root.TryGetProperty("message", out var msg))
        {
            var value = ExtractTextFromElement(msg, skipReasoning: true);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var msgContent))
                {
                    var value = ExtractTextFromElement(msgContent, skipReasoning: true);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                if (choice.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString();
                }
            }
        }

        var fallback = ExtractTextFromElement(root, skipReasoning: true);
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static string? ExtractTextFromElement(JsonElement element, bool skipReasoning)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Array:
            {
                var buffer = new StringBuilder();
                foreach (var item in element.EnumerateArray())
                {
                    var part = ExtractTextFromElement(item, skipReasoning);
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        if (buffer.Length > 0)
                        {
                            buffer.AppendLine();
                        }

                        buffer.Append(part);
                    }
                }

                return buffer.Length == 0 ? null : buffer.ToString();
            }

            case JsonValueKind.Object:
            {
                if (skipReasoning
                    && element.TryGetProperty("type", out var typeEl)
                    && typeEl.ValueKind == JsonValueKind.String)
                {
                    var type = typeEl.GetString();
                    if (type is not null
                        && (type.Equals("reasoning", StringComparison.OrdinalIgnoreCase)
                            || type.Equals("thinking", StringComparison.OrdinalIgnoreCase)
                            || type.Equals("thought", StringComparison.OrdinalIgnoreCase)))
                    {
                        return null;
                    }
                }

                foreach (var key in new[] { "output_text", "text", "content", "message", "response" })
                {
                    if (!element.TryGetProperty(key, out var nested))
                    {
                        continue;
                    }

                    var value = ExtractTextFromElement(nested, skipReasoning);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return null;
            }

            default:
                return null;
        }
    }

    private static string? TryExtractClaudeOutputText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var buffer = new StringBuilder();
        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() == "text"
                && item.TryGetProperty("text", out var text)
                && text.ValueKind == JsonValueKind.String)
            {
                buffer.AppendLine(text.GetString());
            }
        }

        return buffer.Length == 0 ? null : buffer.ToString();
    }

    private static List<string> ParseIndexedOutput(string output, int expectedCount)
    {
        var lines = output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var map = new Dictionary<int, string>();
        var indexLinePattern = new Regex(@"^\[(\d+)\][\t \u3000]*(.*)$", RegexOptions.Compiled);

        foreach (var raw in lines)
        {
            var line = NormalizeOutputLine(raw);
            if (line.Length == 0)
            {
                continue;
            }

            var m = indexLinePattern.Match(line);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                continue;
            }

            var content = m.Groups[2].Value.TrimStart('\t', ' ', '\u3000');
            map[index] = content;
        }

        var result = new List<string>(expectedCount);
        for (var i = 1; i <= expectedCount; i++)
        {
            if (!map.TryGetValue(i, out var text))
            {
                return new List<string>();
            }

            result.Add(text);
        }

        return result;
    }

    private static string NormalizeOutputLine(string raw)
    {
        var line = raw.Trim();
        if (line.Length == 0)
        {
            return line;
        }

        line = line.Replace('［', '[').Replace('］', ']');
        line = line.Replace('\u00A0', ' ').Replace('\u3000', ' ');
        line = line.Replace("\u200B", string.Empty, StringComparison.Ordinal)
            .Replace("\u200C", string.Empty, StringComparison.Ordinal)
            .Replace("\u200D", string.Empty, StringComparison.Ordinal)
            .Replace("\uFEFF", string.Empty, StringComparison.Ordinal);

        return line;
    }

    private sealed record LlmSettings(
        string Endpoint,
        string Model,
        string ApiType,
        string AuthType,
        string? ApiKey,
        string? SystemPrompt,
        int MaxTokens,
        string? Reasoning,
        int RetryCount)
    {
        public static LlmSettings FromEnvironment()
        {
            var endpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT")
                ?? "http://localhost:1234/api/v1/chat";
            var model = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "qwen/qwen3.5-35b-a3b";
            var apiType = (Environment.GetEnvironmentVariable("LLM_API_TYPE") ?? "openai").Trim().ToLowerInvariant();
            if (apiType is not ("openai" or "claude"))
            {
                throw new InvalidOperationException("Unsupported LLM_API_TYPE. Supported values: openai, claude.");
            }

            var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY")
                ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

            var explicitAuth = Environment.GetEnvironmentVariable("LLM_AUTH_TYPE")?.Trim().ToLowerInvariant();
            var endpointIsAzureOpenAi = endpoint.Contains("openai.azure.com", StringComparison.OrdinalIgnoreCase);
            var authType = explicitAuth
                ?? (endpointIsAzureOpenAi ? "azure-rbac" : (!string.IsNullOrWhiteSpace(apiKey) ? "key" : "none"));
            if (authType is not ("none" or "key" or "azure-rbac"))
            {
                throw new InvalidOperationException("Unsupported LLM_AUTH_TYPE. Supported values: none, key, azure-rbac.");
            }

            if (authType == "key" && string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("LLM_AUTH_TYPE=key requires LLM_API_KEY (or OPENAI_API_KEY/ANTHROPIC_API_KEY).");
            }

            var systemPrompt = Environment.GetEnvironmentVariable("LLM_SYSTEM_PROMPT");

            var maxTokensRaw = Environment.GetEnvironmentVariable("LLM_MAX_TOKENS");
            var maxTokens = 2048;
            if (!string.IsNullOrWhiteSpace(maxTokensRaw)
                && int.TryParse(maxTokensRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                && parsed > 0)
            {
                maxTokens = parsed;
            }

            var reasoning = Environment.GetEnvironmentVariable("LLM_REASONING")?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(reasoning))
            {
                reasoning = null;
            }
            else if (reasoning is not ("off" or "low" or "medium" or "high" or "on"))
            {
                throw new InvalidOperationException("Unsupported LLM_REASONING. Supported values: off, low, medium, high, on.");
            }

            var retryRaw = Environment.GetEnvironmentVariable("LLM_RETRY_COUNT");
            var retryCount = 3;
            var retryOverride = LlmRuntimeOverrides.GetRetryCountOverride();
            if (retryOverride is > 0)
            {
                retryCount = retryOverride.Value;
            }
            else if (!string.IsNullOrWhiteSpace(retryRaw)
                && int.TryParse(retryRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRetry)
                && parsedRetry > 0)
            {
                retryCount = parsedRetry;
            }

            retryCount = Math.Clamp(retryCount, 1, 20);

            return new LlmSettings(endpoint, model, apiType, authType, apiKey, systemPrompt, maxTokens, reasoning, retryCount);
        }
    }
}

internal static class LlmRuntimeOverrides
{
    private static readonly AsyncLocal<int?> RetryCountOverride = new();

    public static int? GetRetryCountOverride() => RetryCountOverride.Value;

    public static IDisposable BeginRetryCountScope(int? retryCount)
    {
        var previous = RetryCountOverride.Value;
        RetryCountOverride.Value = retryCount;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly int? _previous;

        public Scope(int? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            RetryCountOverride.Value = _previous;
        }
    }
}

internal static class RuntimeEnvironmentOverrides
{
    public static IDisposable Begin(IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null || overrides.Count == 0)
        {
            return NoopDisposable.Instance;
        }

        var previous = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in overrides)
        {
            previous[kv.Key] = Environment.GetEnvironmentVariable(kv.Key);
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
        }

        return new Scope(previous);
    }

    public static Dictionary<string, string>? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOf('=');
            if (idx <= 0 || idx == part.Length - 1)
            {
                throw new InvalidOperationException($"Invalid env override segment: {part}. Use KEY=VALUE;KEY2=VALUE2 format.");
            }

            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim();
            if (key.Length == 0)
            {
                throw new InvalidOperationException($"Invalid env override key in segment: {part}");
            }

            map[key] = value;
        }

        return map;
    }

    private sealed class Scope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, string?> _previous;

        public Scope(IReadOnlyDictionary<string, string?> previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            foreach (var kv in _previous)
            {
                Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}

internal static class SrtSerializer
{
    public static List<SubtitleCue> Parse(string input)
    {
        var lines = input.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var cues = new List<SubtitleCue>();
        var i = 0;

        while (i < lines.Length)
        {
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
            {
                i++;
            }

            if (i >= lines.Length)
            {
                break;
            }

            if (!int.TryParse(lines[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                i++;
                continue;
            }

            i++;
            if (i >= lines.Length)
            {
                break;
            }

            var timeLine = lines[i];
            var split = timeLine.Split("-->", StringSplitOptions.TrimEntries);
            if (split.Length != 2)
            {
                i++;
                continue;
            }

            var start = ParseSrtTime(split[0]);
            var end = ParseSrtTime(split[1]);
            i++;

            var textLines = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                textLines.Add(lines[i]);
                i++;
            }

            cues.Add(new SubtitleCue(index, start, end, textLines));
        }

        return cues;
    }

    public static string Serialize(List<SubtitleCue> cues)
    {
        var sb = new StringBuilder();
        foreach (var cue in cues.OrderBy(x => x.Index))
        {
            sb.AppendLine(cue.Index.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine($"{FormatSrtTime(cue.Start)} --> {FormatSrtTime(cue.End)}");
            foreach (var line in cue.Lines)
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static TimeSpan ParseSrtTime(string raw)
    {
        var normalized = raw.Replace(',', '.');
        return TimeSpan.ParseExact(normalized, "hh\\:mm\\:ss\\.fff", CultureInfo.InvariantCulture);
    }

    private static string FormatSrtTime(TimeSpan value)
        => value.ToString("hh\\:mm\\:ss\\,fff", CultureInfo.InvariantCulture);
}

internal sealed record SubtitleCue(int Index, TimeSpan Start, TimeSpan End, List<string> Lines);

internal sealed record SubtitleTrack(int StreamIndex, int SubtitleOrder, string Language, string Title);

internal sealed record ProbeResult(string Input, string TargetLanguage, bool HasTargetLanguage, List<SubtitleTrack> Tracks);

internal sealed record SubtitleCandidate(int Rank, string Language, double Score, string Name, string Source);

internal sealed record OpenSubtitlesResult(string Input, string TargetLanguage, List<SubtitleCandidate> Candidates);

internal sealed record ExtractionResult(string Input, string OutputPath, string SelectedLanguage, string Strategy);

internal sealed record SubtitleGroup(int GroupIndex, List<SubtitleCue> Cues);

internal sealed record GroupTranslationResult(int GroupIndex, string ParaphraseSummary, string ParaphraseHistory, List<SubtitleCue> Cues);

internal sealed record WorkflowResult(string Status, string Branch, string OutputPath, List<GroupTranslationResult> Groups);

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true
    };
}

internal static class ErrorSnapshotWriter
{
    private static readonly AsyncLocal<string?> TranslateHistoryDirectory = new();

    public static string Write(string prefix, IReadOnlyDictionary<string, string?> sections)
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "tmp");
        Directory.CreateDirectory(dir);

        var filePath = Path.Combine(
            dir,
            $"{prefix}.{DateTimeOffset.Now:yyyyMMdd_HHmmss_fff}.{Guid.NewGuid():N}.log");

        var sb = new StringBuilder();
        sb.AppendLine($"timestamp: {DateTimeOffset.Now:O}");
        sb.AppendLine($"prefix: {prefix}");
        sb.AppendLine();

        foreach (var kv in sections)
        {
            sb.AppendLine($"===== {kv.Key} =====");
            sb.AppendLine(kv.Value ?? string.Empty);
            sb.AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    public static IDisposable BeginTranslateHistoryScope(string outputPath)
    {
        var current = TranslateHistoryDirectory.Value;

        var outputDir = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = Directory.GetCurrentDirectory();
        }

        var historyDir = Path.Combine(outputDir, ".translatehistory");
        Directory.CreateDirectory(historyDir);
        TranslateHistoryDirectory.Value = historyDir;
        return new HistoryScope(current);
    }

    public static string WriteMarkdown(string prefix, IReadOnlyDictionary<string, string?> sections, string? fileTag = null)
    {
        var dir = TranslateHistoryDirectory.Value;
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(Directory.GetCurrentDirectory(), ".translatehistory");
        }

        Directory.CreateDirectory(dir);

        var safeTag = string.IsNullOrWhiteSpace(fileTag)
            ? null
            : SanitizeFileTag(fileTag);

        var filePath = Path.Combine(
            dir,
            string.IsNullOrWhiteSpace(safeTag)
                ? $"{prefix}.{DateTimeOffset.Now:yyyyMMdd_HHmmss_fff}.{Guid.NewGuid():N}.md"
                : $"{prefix}.{safeTag}.{DateTimeOffset.Now:yyyyMMdd_HHmmss_fff}.{Guid.NewGuid():N}.md");

        var sb = new StringBuilder();
        sb.AppendLine($"# {prefix}");
        sb.AppendLine();
        sb.AppendLine($"- timestamp: {DateTimeOffset.Now:O}");
        sb.AppendLine();

        foreach (var kv in sections)
        {
            sb.AppendLine($"## {kv.Key}");
            sb.AppendLine();
            sb.AppendLine("```text");
            sb.AppendLine(kv.Value ?? string.Empty);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        return filePath;
    }

    private static string SanitizeFileTag(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (invalid.Contains(ch) || char.IsWhiteSpace(ch))
            {
                buffer.Append('-');
            }
            else
            {
                buffer.Append(ch);
            }
        }

        return buffer.ToString().Trim('-');
    }

    private sealed class HistoryScope : IDisposable
    {
        private readonly string? _previous;

        public HistoryScope(string? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            TranslateHistoryDirectory.Value = _previous;
        }
    }
}

internal static class CliRuntimeLog
{
    private static readonly object Sync = new();
    private static readonly Stopwatch Runtime = new();
    private static long _sequence;
    private static bool _enabled;

    public static void Configure(bool enabled)
    {
        _enabled = enabled;
        lock (Sync)
        {
            _sequence = 0;
            Runtime.Reset();
            Runtime.Start();
        }
    }

    public static void Info(string area, string message) => Write("INFO", area, message);

    public static void Warn(string area, string message) => Write("WARN", area, message);

    public static void Error(string area, string message) => Write("ERROR", area, message);

    public static IDisposable BeginScope(string area, string message)
    {
        Write("BEGIN", area, message);
        return new Scope(area, message);
    }

    private static void Write(string level, string area, string message)
    {
        if (!_enabled)
        {
            return;
        }

        lock (Sync)
        {
            var seq = ++_sequence;
            var elapsed = Runtime.Elapsed;
            Console.Error.WriteLine($"[{elapsed:hh\\:mm\\:ss\\.fff} #{seq:0000}] [{level}] [{area}] {message}");
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly string _area;
        private readonly string _message;
        private bool _disposed;

        public Scope(string area, string message)
        {
            _area = area;
            _message = message;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sw.Stop();
            Write("END", _area, $"{_message} (elapsed={_sw.Elapsed:hh\\:mm\\:ss\\.fff})");
        }
    }
}
