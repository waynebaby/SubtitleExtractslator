using System.Globalization;
using System.Threading;

namespace SubtitleExtractslator.Cli;

internal sealed class WorkflowOrchestrator
{
    private readonly SubtitleOperations _ops;
    private readonly TranslationPipeline _translator;

    public WorkflowOrchestrator(ModeContext modeContext)
    {
        _ops = new SubtitleOperations(modeContext);
        _translator = new TranslationPipeline(modeContext, new ExternalTranslationProvider(), new SamplingTranslationProvider());
    }

    public Task<ProbeResult> ProbeAsync(string input, string targetLanguage)
        => _ops.ProbeTracksAsync(input, targetLanguage);

    public Task<OpenSubtitlesResult> SearchOpenSubtitlesAsync(
        string input,
        string targetLanguage,
        OpenSubtitlesSearchQueries queries,
        OpenSubtitlesCredentials? credentials)
        => _ops.SearchOpenSubtitlesAsync(input, targetLanguage, queries, credentials);

    public Task<OpenSubtitlesDownloadResult> DownloadOpenSubtitleAsync(
        string input,
        string targetLanguage,
        string output,
        int candidateRank = 1,
        string? fileId = null,
        OpenSubtitlesCredentials? credentials = null)
        => _ops.DownloadOpenSubtitleAsync(input, targetLanguage, output, candidateRank, fileId, credentials);

    public Task<OpenSubtitlesDownloadResult> DownloadOpenSubtitleByFileIdAsync(
        string fileId,
        string output,
        OpenSubtitlesCredentials? credentials = null)
        => _ops.DownloadOpenSubtitleByFileIdAsync(fileId, output, credentials);

    public Task<ExtractionResult> ExtractSubtitleAsync(string input, string output, string preferredLanguage)
        => _ops.ExtractSubtitleAsync(input, output, preferredLanguage);

    public async Task<WorkflowResult> TranslateAsync(
        string input,
        string targetLanguage,
        string output,
        int? cuesPerGroupOverride = null,
        int? bodySizeOverride = null,
        int? llmRetryCountOverride = null,
        IReadOnlyDictionary<string, string>? envOverrides = null)
    {
        using var workflowScope = CliRuntimeLog.BeginScope("workflow", $"Start translate input={input} target={targetLanguage} output={output}");
        using var responseHealthScope = ResponseSizeHealthMonitor.BeginTaskScope($"translate-{Guid.NewGuid():N}");
        using var envScope = RuntimeEnvironmentOverrides.Begin(envOverrides);
        using var historyScope = ErrorSnapshotWriter.BeginTranslateHistoryScope(output);
        using var llmRetryScope = LlmRuntimeOverrides.BeginRetryCountScope(llmRetryCountOverride);

        if (!Path.GetExtension(input).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("translate only accepts subtitle input (*.srt). Use other tools for probe/search/extract first.");
        }

        var groupResults = await TranslateSubtitleToOutputAsync(input, targetLanguage, output, cuesPerGroupOverride, bodySizeOverride);
        return new WorkflowResult("completed", "translate-only", output, groupResults, null);
    }

    public async Task<WorkflowResult> RunWorkflowAsync(
        string input,
        string targetLanguage,
        string output,
        int? cuesPerGroupOverride = null,
        int? bodySizeOverride = null,
        int? llmRetryCountOverride = null,
        string? muxOutputPath = null,
        OpenSubtitlesCredentials? openSubtitlesCredentials = null,
        IReadOnlyDictionary<string, string>? envOverrides = null)
    {
        using var workflowScope = CliRuntimeLog.BeginScope("workflow", $"Start run_workflow input={input} target={targetLanguage} output={output}");
        using var responseHealthScope = ResponseSizeHealthMonitor.BeginTaskScope($"workflow-{Guid.NewGuid():N}");
        using var envScope = RuntimeEnvironmentOverrides.Begin(envOverrides);
        using var historyScope = ErrorSnapshotWriter.BeginTranslateHistoryScope(output);
        using var llmRetryScope = LlmRuntimeOverrides.BeginRetryCountScope(llmRetryCountOverride);
        var probe = await ProbeAsync(input, targetLanguage);
        CliRuntimeLog.Info("workflow", $"Probe completed. tracks={probe.Tracks.Count} hasTarget={probe.HasTargetLanguage}");
        if (probe.HasTargetLanguage)
        {
            CliRuntimeLog.Info("workflow", "Target subtitle already exists. Workflow exits early.");
            return new WorkflowResult("completed", "target-track-exists", output, new List<GroupTranslationResult>(), null);
        }

        var searchQueries = SubtitleOperations.BuildSearchQueriesFromInput(input);
        var openResult = await SearchOpenSubtitlesAsync(input, targetLanguage, searchQueries, openSubtitlesCredentials);
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
        if (accepted)
        {
            try
            {
                var openSubtitlePath = BuildTemporaryExtractionPath(output, NormalizeLanguageToken(targetLanguage));
                subtitlePath = await _ops.DownloadOpenSubtitleAsync(openResult.Candidates[0], openSubtitlePath, openSubtitlesCredentials);
                CliRuntimeLog.Info("workflow", $"OpenSubtitles adoption completed. subtitlePath={subtitlePath}");
            }
            catch (Exception ex)
            {
                CliRuntimeLog.Warn("workflow", $"OpenSubtitles adoption failed. Falling back to local extraction. reason={ex.Message}");
                accepted = false;
            }
        }

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

        var groupResults = await TranslateSubtitleToOutputAsync(subtitlePath, targetLanguage, output, cuesPerGroupOverride, bodySizeOverride);

        string? muxedOutput = null;
        if (!string.IsNullOrWhiteSpace(muxOutputPath))
        {
            CliRuntimeLog.Info("workflow", $"Mux output requested. inputMedia={input} subtitle={output} muxOutput={muxOutputPath}");
            muxedOutput = await _ops.MuxSubtitleIntoVideoAsync(input, output, muxOutputPath, targetLanguage);
            CliRuntimeLog.Info("workflow", $"Mux output written: {muxedOutput}");
        }

        return new WorkflowResult("completed", accepted ? "opensubtitles-adopted" : "local-extraction", output, groupResults, muxedOutput);
    }

    private async Task<List<GroupTranslationResult>> TranslateSubtitleToOutputAsync(
        string subtitlePath,
        string targetLanguage,
        string output,
        int? cuesPerGroupOverride,
        int? bodySizeOverride)
    {
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
        return groupResults;
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
            return 4;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return 4;
        }

        return Math.Clamp(parsed, 1, 32);
    }

    private static int ResolveCuesPerGroup(int? overrideValue)
    {
        var parsed = overrideValue ?? ResolvePositiveIntFromEnvironment("SUBTITLEEXTRACTSLATOR_CUES_PER_GROUP", 5);
        return Math.Clamp(parsed, 1, 500);
    }

    private static int ResolveTranslationBodySize(int? overrideValue)
    {
        var parsed = overrideValue ?? ResolvePositiveIntFromEnvironment("SUBTITLEEXTRACTSLATOR_TRANSLATION_BODY_SIZE", 20);
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
        var directory = RuntimePathPolicy.GetIntermediateDirectory("subtitles");
        Directory.CreateDirectory(directory);
        var baseName = Path.GetFileNameWithoutExtension(output);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "subtitle";
        }

        return Path.Combine(directory, $"{baseName}.{sourceLanguage}.{Guid.NewGuid():N}.srt");
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
