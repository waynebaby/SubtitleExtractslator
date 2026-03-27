using System.ComponentModel;
using System.Diagnostics;
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
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace SubtitleExtractslator.Cli;

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
    public static List<SubtitleGroup> Group(List<SubtitleCue> cues, int cuesPerGroup = 5)
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
            var mcpServerAvailable = McpSamplingRuntimeContext.CurrentServer is not null;
            if (!mcpServerAvailable)
            {
                CliRuntimeLog.Warn(
                    "translate",
                    "Mode=MCP but McpServer instance is unavailable (Translate parameter injection failed or missing). Sampling-only policy active; returning error.");
                throw new InvalidOperationException(
                    "MCP translation requires sampling, but McpServer instance is unavailable. "
                    + "Under sampling-only policy, external fallback is disabled.");
            }
            else
            {
                CliRuntimeLog.Info("translate", "Mode=MCP. McpServer instance available. Sampling-only policy active.");
                translated = await _samplingProvider.TranslateIndexedAsync(sourceCueTexts, targetLanguage, contextGuide, contextHint);
                if (translated.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Sampling provider returned empty translation result in MCP mode. "
                        + "Under sampling-only policy, external fallback is disabled.");
                }
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
        var mainIndexed = mainGroup.Cues
            .OrderBy(c => c.Index)
            .Select((c, i) => $"[{i + 1}]\t{FlattenCueLines(c.Lines)}")
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
        AppendPlainLines(sb, mainIndexed, "    ");
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
    private sealed class SamplingRequestException : InvalidOperationException
    {
        public SamplingRequestException(string message, bool isOversized)
            : base(message)
        {
            IsOversized = isOversized;
        }

        public bool IsOversized { get; }
    }

    public async Task<IReadOnlyList<string>> TranslateIndexedAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string contextParaphrase,
        string contextHint)
    {
        using var scope = CliRuntimeLog.BeginScope("sampling", $"Start MCP sampling translation call. lines={lines.Count} target={targetLanguage}");
        if (lines.Count == 0)
        {
            CliRuntimeLog.Info("sampling", "No lines to translate. Returning empty result.");
            return Array.Empty<string>();
        }

        var server = McpSamplingRuntimeContext.CurrentServer;
        if (server is null)
        {
            CliRuntimeLog.Warn("sampling", "MCP sampling server context is unavailable (Translate McpServer parameter injection failed or missing). Sampling-only policy active; returning error.");
            throw new InvalidOperationException(
                "MCP sampling server context is unavailable. "
                + "Under sampling-only policy, external fallback is disabled.");
        }

        var settings = ExternalTranslationProvider.LlmSettings.FromEnvironment();
        var systemPrompt = settings.SystemPrompt
            ?? "You are a subtitle translator. Use the provided context sections to reason first, then output only the line-by-line translation for the target lines. Never add commentary in final output. Keep every index and return exactly one translated line for each input index.";
        var maxAttempts = settings.RetryCount;
        var qualifiedForHealth = ExternalTranslationProvider.IsQualifiedHealthSample(contextParaphrase);
        var healthBucket = ResponseSizeHealthMonitor.BuildBucketKey(settings.Model, targetLanguage);
        CliRuntimeLog.Info(
            "sampling",
            $"Resolved settings. model={settings.Model} retryCount={settings.RetryCount} qualifiedHealth={qualifiedForHealth} bucket={healthBucket}");

        var previousAttemptOversized = false;
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var health = ResponseSizeHealthMonitor.GetSnapshot(healthBucket);
            var indexedInput = ExternalTranslationProvider.BuildIndexedInput(
                lines,
                targetLanguage,
                contextParaphrase,
                includeRetryOversizeHint: previousAttemptOversized);
            if (previousAttemptOversized)
            {
                CliRuntimeLog.Warn("sampling", "Retry oversize hint injected into sampling prompt for this attempt.");
            }

            CliRuntimeLog.Info(
                "sampling",
                $"Sampling request attempt {attempt}/{maxAttempts}. healthSamples={health.SampleCount} healthAvgBytes={health.AverageBytes:F1} streamGuardBytes={health.GuardThresholdBytes}");

            try
            {
                var samplingRequest = new CreateMessageRequestParams
                {
                    SystemPrompt = systemPrompt,
                    MaxTokens = settings.MaxTokens,
                    Temperature = 0.2f,
                    Messages =
                    [
                        new SamplingMessage
                        {
                            Role = Role.User,
                            Content =
                            [
                                new TextContentBlock { Text = indexedInput }
                            ]
                        }
                    ],
                    ModelPreferences = string.IsNullOrWhiteSpace(settings.Model)
                        ? null
                        : new ModelPreferences
                        {
                            Hints =
                            [
                                new ModelHint { Name = settings.Model }
                            ]
                        }
                };

                var sampled = await server.SampleAsync(samplingRequest);
                var sampledText = string.Join(
                    "\n",
                    sampled.Content
                        .OfType<TextContentBlock>()
                        .Select(x => x.Text)
                        .Where(x => !string.IsNullOrWhiteSpace(x)));

                if (string.IsNullOrWhiteSpace(sampledText))
                {
                    throw new InvalidOperationException("Sampling response content was empty.");
                }

                var sampledBytes = Encoding.UTF8.GetByteCount(sampledText);
                if (sampledBytes > health.GuardThresholdBytes)
                {
                    throw new SamplingRequestException(
                        $"Sampling response exceeded health guard threshold ({sampledBytes} bytes > {health.GuardThresholdBytes} bytes).",
                        isOversized: true);
                }

                var translated = ExternalTranslationProvider.ParseIndexedOutput(sampledText, lines.Count);
                if (translated.Count != lines.Count)
                {
                    throw new InvalidOperationException(
                        $"Sampling output line count mismatch. parsedLines={translated.Count} expectedLines={lines.Count}.");
                }

                if (qualifiedForHealth)
                {
                    ResponseSizeHealthMonitor.Record(healthBucket, sampledBytes, qualifiedForHealth);
                    var postRecord = ResponseSizeHealthMonitor.GetSnapshot(healthBucket);
                    CliRuntimeLog.Info(
                        "sampling",
                        $"Health sample recorded. responseBytes={sampledBytes} windowCount={postRecord.SampleCount} windowAvgBytes={postRecord.AverageBytes:F1} nextGuardBytes={postRecord.GuardThresholdBytes}");
                }
                else
                {
                    CliRuntimeLog.Info("sampling", "Health sample skipped for this response because context is not qualified.");
                }

                CliRuntimeLog.Info("sampling", $"Sampling translation succeeded. parsedLines={translated.Count}");
                return translated;
            }
            catch (Exception ex)
            {
                previousAttemptOversized = ex is SamplingRequestException sampleEx && sampleEx.IsOversized;
                if (previousAttemptOversized)
                {
                    CliRuntimeLog.Warn("sampling", "Failure classified as oversized sampling response. Next retry will include concise-reasoning warning.");
                }

                lastError = ex;
                CliRuntimeLog.Warn("sampling", $"Attempt {attempt}/{maxAttempts} failed under sampling-only policy: {ex.Message}");
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt));
                    continue;
                }
            }
        }

        throw new InvalidOperationException($"MCP sampling translation failed after {maxAttempts} attempts.", lastError);
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

        var maxAttempts = settings.RetryCount;
        var qualifiedForHealth = IsQualifiedHealthSample(contextParaphrase);
        var healthBucket = ResponseSizeHealthMonitor.BuildBucketKey(settings.Model, targetLanguage);
        CliRuntimeLog.Info(
            "llm",
            $"Response health qualification: qualified={qualifiedForHealth} requiresPreviousAndFollowingContext=true bucket={healthBucket}");
        var previousAttemptOversized = false;
        int? lastSeenGuardThresholdBytes = null;
        Exception? lastError = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var health = ResponseSizeHealthMonitor.GetSnapshot(healthBucket);
            if (lastSeenGuardThresholdBytes.HasValue && lastSeenGuardThresholdBytes.Value != health.GuardThresholdBytes)
            {
                var delta = health.GuardThresholdBytes - lastSeenGuardThresholdBytes.Value;
                var thresholdMultiplier = lastSeenGuardThresholdBytes.Value > 0
                    ? ((double)health.GuardThresholdBytes / lastSeenGuardThresholdBytes.Value).ToString("F3", CultureInfo.InvariantCulture)
                    : "n/a";
                var thresholdMultiplierPercent = lastSeenGuardThresholdBytes.Value > 0
                    ? ((double)health.GuardThresholdBytes / lastSeenGuardThresholdBytes.Value * 100.0).ToString("F1", CultureInfo.InvariantCulture)
                    : "n/a";
                CliRuntimeLog.Info(
                    "llm",
                    $"Health guard threshold changed before attempt. old={lastSeenGuardThresholdBytes.Value} new={health.GuardThresholdBytes} delta={delta} thresholdMultiplier={thresholdMultiplier}x thresholdMultiplierPercent={thresholdMultiplierPercent}% baselineMultiplier={ResponseSizeHealthMonitor.GuardMultiplierPercent}% bucket={healthBucket}");
            }

            var indexedInput = BuildIndexedInput(
                lines,
                targetLanguage,
                contextParaphrase,
                includeRetryOversizeHint: previousAttemptOversized);
            CliRuntimeLog.Info(
                "llm",
                $"Prompt built. chars={indexedInput.Length} healthSamples={health.SampleCount} healthAvgBytes={health.AverageBytes:F1} healthValid={health.IsValid} streamGuardBytes={health.GuardThresholdBytes.ToString(CultureInfo.InvariantCulture)} hardCapBytes={ResponseSizeHealthMonitor.AbsoluteGuardBytes} guardBaselineMultiplier={ResponseSizeHealthMonitor.GuardMultiplierPercent}%");
            lastSeenGuardThresholdBytes = health.GuardThresholdBytes;
            if (previousAttemptOversized)
            {
                CliRuntimeLog.Warn("llm", "Retry oversize hint injected into prompt for this attempt.");
            }
            CliRuntimeLog.Info("llm", $"LLM request attempt {attempt}/{maxAttempts}.");
            try
            {
                var execution = await ExecutePromptAsync(settings, systemPrompt, indexedInput, health.GuardThresholdBytes);
                var output = execution.OutputText;
                var responseToGuardRatio = health.GuardThresholdBytes > 0
                    ? ((double)execution.ResponseBytes / health.GuardThresholdBytes).ToString("F3", CultureInfo.InvariantCulture)
                    : "n/a";
                CliRuntimeLog.Info("llm", $"Output text extracted. chars={output?.Length ?? 0} outputTokens={execution.OutputTokenCount?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} responseBytes={execution.ResponseBytes} guardBytes={health.GuardThresholdBytes} sizeToGuardMultiplier={responseToGuardRatio}x");
                var responseToGuardPercent = health.GuardThresholdBytes > 0
                    ? ((double)execution.ResponseBytes / health.GuardThresholdBytes * 100.0).ToString("F1", CultureInfo.InvariantCulture)
                    : "n/a";
                CliRuntimeLog.Info("llm", $"Response size usage. responseBytes={execution.ResponseBytes} guardBytes={health.GuardThresholdBytes} sizeToGuardPercent={responseToGuardPercent}%");

                if (qualifiedForHealth)
                {
                    ResponseSizeHealthMonitor.Record(healthBucket, execution.ResponseBytes, qualifiedForHealth);
                    var postRecord = ResponseSizeHealthMonitor.GetSnapshot(healthBucket);
                    if (postRecord.GuardThresholdBytes != health.GuardThresholdBytes)
                    {
                        var delta = postRecord.GuardThresholdBytes - health.GuardThresholdBytes;
                        var thresholdMultiplier = health.GuardThresholdBytes > 0
                            ? ((double)postRecord.GuardThresholdBytes / health.GuardThresholdBytes).ToString("F3", CultureInfo.InvariantCulture)
                            : "n/a";
                        var thresholdMultiplierPercent = health.GuardThresholdBytes > 0
                            ? ((double)postRecord.GuardThresholdBytes / health.GuardThresholdBytes * 100.0).ToString("F1", CultureInfo.InvariantCulture)
                            : "n/a";
                        CliRuntimeLog.Info(
                            "llm",
                            $"Health guard threshold changed after sample record. old={health.GuardThresholdBytes} new={postRecord.GuardThresholdBytes} delta={delta} thresholdMultiplier={thresholdMultiplier}x thresholdMultiplierPercent={thresholdMultiplierPercent}% baselineMultiplier={ResponseSizeHealthMonitor.GuardMultiplierPercent}% bucket={healthBucket}");
                    }

                    var postRecordSizeToGuardRatio = postRecord.GuardThresholdBytes > 0
                        ? ((double)execution.ResponseBytes / postRecord.GuardThresholdBytes).ToString("F3", CultureInfo.InvariantCulture)
                        : "n/a";
                    CliRuntimeLog.Info(
                        "llm",
                        $"Health sample recorded. responseBytes={execution.ResponseBytes} windowCount={postRecord.SampleCount} windowAvgBytes={postRecord.AverageBytes:F1} nextGuardBytes={postRecord.GuardThresholdBytes.ToString(CultureInfo.InvariantCulture)} hardCapBytes={ResponseSizeHealthMonitor.AbsoluteGuardBytes} baselineMultiplier={ResponseSizeHealthMonitor.GuardMultiplierPercent}% sizeToNextGuardMultiplier={postRecordSizeToGuardRatio}x");
                    lastSeenGuardThresholdBytes = postRecord.GuardThresholdBytes;
                }
                else
                {
                    CliRuntimeLog.Info("llm", $"Health sample skipped for this response. responseBytes={execution.ResponseBytes}");
                }

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
                        ["responseBytes"] = execution.ResponseBytes.ToString(CultureInfo.InvariantCulture),
                        ["healthSampleQualified"] = qualifiedForHealth ? "true" : "false",
                        ["healthBucket"] = healthBucket,
                        ["healthSampleCount"] = health.SampleCount.ToString(CultureInfo.InvariantCulture),
                        ["healthAverageBytes"] = health.AverageBytes.ToString("F1", CultureInfo.InvariantCulture),
                        ["healthGuardThresholdBytes"] = health.GuardThresholdBytes.ToString(CultureInfo.InvariantCulture),
                        ["healthGuardHardCapBytes"] = ResponseSizeHealthMonitor.AbsoluteGuardBytes.ToString(CultureInfo.InvariantCulture),
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
                var responseBytesFromError = ex is LlmRequestException reqEx3 ? reqEx3.ResponseBytes?.ToString(CultureInfo.InvariantCulture) : null;
                var guardBytesFromError = ex is LlmRequestException reqEx4 ? reqEx4.GuardThresholdBytes?.ToString(CultureInfo.InvariantCulture) : null;
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
                        ["responseBytes"] = responseBytesFromError,
                        ["healthGuardThresholdBytes"] = guardBytesFromError,
                        ["healthSampleQualified"] = qualifiedForHealth ? "true" : "false",
                        ["healthBucket"] = healthBucket,
                        ["healthGuardHardCapBytes"] = ResponseSizeHealthMonitor.AbsoluteGuardBytes.ToString(CultureInfo.InvariantCulture),
                        ["systemPrompt"] = systemPrompt,
                        ["inputPrompt"] = indexedInput,
                        ["error"] = ex.ToString()
                    },
                    BuildSnapshotTag(contextHint, attempt, maxAttempts));
                CliRuntimeLog.Warn("llm", $"LLM I/O error dump written: {ioDumpPath}");

                previousAttemptOversized = ex is LlmRequestException sizeEx && sizeEx.IsOversized;
                if (previousAttemptOversized)
                {
                    CliRuntimeLog.Warn("llm", "Failure classified as oversized stream response. Next retry will include concise-reasoning warning.");
                }

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

    private async Task<LlmExecutionResult> ExecutePromptAsync(
        LlmSettings settings,
        string systemPrompt,
        string userPrompt,
        int? streamGuardThresholdBytes)
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
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        body = await ReadResponseBodyWithGuardAsync(response, streamGuardThresholdBytes);
        CliRuntimeLog.Info("llm", $"Received response. status={(int)response.StatusCode} bodyChars={body.Length}");
        var reasoningText = TryExtractReasoningText(body);
        var responseBytes = Encoding.UTF8.GetByteCount(body);
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
                reasoningText,
                isOversized: false,
                responseBytes,
                streamGuardThresholdBytes);
        }

        var outputText = TryExtractOutputText(body, settings.ApiType) ?? string.Empty;
        var outputTokenCount = TryExtractOutputTokenCount(body, settings.ApiType)
            ?? ApproximateTokenCount(outputText);
        return new LlmExecutionResult(outputText, outputTokenCount, reasoningText, responseBytes);
    }

    private static async Task<string> ReadResponseBodyWithGuardAsync(HttpResponseMessage response, int? guardThresholdBytes)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        var totalBytes = 0;
        var nextProgressLog = 64 * 1024;

        CliRuntimeLog.Info(
            "llm",
            $"Stream read started. guardThresholdBytes={(guardThresholdBytes?.ToString(CultureInfo.InvariantCulture) ?? "n/a")}");

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read <= 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes >= nextProgressLog)
            {
                CliRuntimeLog.Info("llm", $"Stream progress. downloadedBytes={totalBytes}");
                nextProgressLog += 64 * 1024;
            }

            if (guardThresholdBytes is > 0 && totalBytes > guardThresholdBytes.Value)
            {
                CliRuntimeLog.Warn(
                    "llm",
                    $"Stream guard triggered. downloadedBytes={totalBytes} guardThresholdBytes={guardThresholdBytes.Value}. Aborting stream.");
                var partialBody = Encoding.UTF8.GetString(ms.ToArray());
                var reasoningText = TryExtractReasoningText(partialBody);
                throw new LlmRequestException(
                    $"LLM streaming response exceeded health guard threshold ({totalBytes} bytes > {guardThresholdBytes.Value} bytes). Aborting and retrying.",
                    partialBody,
                    reasoningText,
                    isOversized: true,
                    totalBytes,
                    guardThresholdBytes);
            }

            await ms.WriteAsync(buffer.AsMemory(0, read));
        }

        CliRuntimeLog.Info("llm", $"Stream read completed. downloadedBytes={totalBytes}");
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    internal static string BuildIndexedInput(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string contextParaphrase,
        bool includeRetryOversizeHint)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Translate the following MAIN GROUP subtitle cues to {targetLanguage}.");
        sb.AppendLine("STRICT FORMAT RULES:");
        sb.AppendLine("1) Keep numbering unchanged and contiguous. Do not skip any index.");
        sb.AppendLine("2) Keep one output line for each input cue. Total output lines must equal total input lines.");
        sb.AppendLine("3) Never delete non-dialogue lines, including CC/music/sound/environment descriptions.");
        sb.AppendLine("4) Preserve conversational atmosphere, character voice, and humor. Keep jokes and references where possible; do not dilute punchlines.");
        sb.AppendLine("5) If source and target language word order differ, you may reorder phrasing across adjacent indexed lines to make natural target-language syntax. Keep the same total indexed line count and subtitle rhythm.");
        sb.AppendLine("6) For split long sentences, prioritize natural target-language reading flow over rigid source order. You may move modifiers/clauses between nearby lines as long as meaning, tone, and pacing are preserved.");
        sb.AppendLine("7) Think silently in this order before output: (a) first create a brief holistic paraphrase of the whole MAIN GROUP in your mind, (b) then refine line-by-line details and references, (c) preserve jokes/puns/religion/sexual/pop-culture effects, (d) align each output line to its input index.");
        sb.AppendLine("8) Context is provided in XML sections. Use <previous_context> and <following_context> only for guidance. Translate only the indexed lines inside <main_section>.");
        sb.AppendLine("9) Output translated result only. Never echo source text. Never output bilingual comparison pairs such as 'source -> translation', 'source => translation', or 'source : translation'.");
        sb.AppendLine("10) Preserve inline special formatting whenever possible (for example HTML/XML tags). Keep tag structure and attributes unchanged; translate only inner text. Example: [99]\t<font face=\"Serif\" size=\"18\">The point is I choose you.</font>");
        if (includeRetryOversizeHint)
        {
            sb.AppendLine("IMPORTANT RETRY NOTE: The previous attempt may have failed due to overly long reasoning. Keep your reasoning concise and do not let your thoughts sprawl.");
        }
        sb.AppendLine("Return ONLY numbered lines in the exact format: [index]\ttranslated text (translated text only, no source text).");
        sb.AppendLine("Context sections:");
        sb.AppendLine(contextParaphrase);

        return sb.ToString();
    }

    internal static bool IsQualifiedHealthSample(string contextParaphrase)
    {
        return HasNonEmptyContextSection(contextParaphrase, "previous_context")
            && HasNonEmptyContextSection(contextParaphrase, "following_context");
    }

    private static bool HasNonEmptyContextSection(string xml, string sectionName)
    {
        var markerStart = $"<{sectionName}>";
        var markerEnd = $"</{sectionName}>";
        var start = xml.IndexOf(markerStart, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return false;
        }

        start += markerStart.Length;
        var end = xml.IndexOf(markerEnd, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            return false;
        }

        var body = xml[start..end].Trim();
        return body.Length > 0 && !body.Equals("(none)", StringComparison.OrdinalIgnoreCase);
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

    private sealed record LlmExecutionResult(string OutputText, int? OutputTokenCount, string? ReasoningText, int ResponseBytes);

    private sealed class LlmRequestException : InvalidOperationException
    {
        public LlmRequestException(
            string message,
            string? responseBody,
            string? reasoningText,
            bool isOversized,
            int? responseBytes,
            int? guardThresholdBytes)
            : base(message)
        {
            ResponseBody = responseBody;
            ReasoningText = reasoningText;
            IsOversized = isOversized;
            ResponseBytes = responseBytes;
            GuardThresholdBytes = guardThresholdBytes;
        }

        public string? ResponseBody { get; }

        public string? ReasoningText { get; }

        public bool IsOversized { get; }

        public int? ResponseBytes { get; }

        public int? GuardThresholdBytes { get; }
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

    internal static List<string> ParseIndexedOutput(string output, int expectedCount)
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

    internal sealed record LlmSettings(
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
            var model = Environment.GetEnvironmentVariable("LLM_MODEL") ?? "qwen3.5-9b-uncensored-hauhaucs-aggressive";
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

internal static class McpSamplingRuntimeContext
{
    private static readonly AsyncLocal<McpServer?> Current = new();

    public static McpServer? CurrentServer => Current.Value;

    public static IDisposable BeginServerScope(McpServer? server)
    {
        var previous = Current.Value;
        Current.Value = server;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly McpServer? _previous;

        public Scope(McpServer? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            Current.Value = _previous;
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

internal static class ResponseSizeHealthMonitor
{
    public const int AbsoluteGuardBytes = 64 * 1024;
    public const double GuardMultiplier = 2.0;
    public const int GuardMultiplierPercent = 200;
    private static readonly object Sync = new();
    private static readonly Dictionary<string, Queue<int>> QualifiedWindowsByBucket = new(StringComparer.OrdinalIgnoreCase);
    private static readonly AsyncLocal<string?> CurrentTaskScopeId = new();
    private const int WindowSize = 5;

    public static IDisposable BeginTaskScope(string taskScopeId)
    {
        var previous = CurrentTaskScopeId.Value;
        CurrentTaskScopeId.Value = string.IsNullOrWhiteSpace(taskScopeId) ? null : taskScopeId.Trim();
        return new Scope(previous);
    }

    public static string BuildBucketKey(string model, string targetLanguage)
    {
        var taskScopeId = string.IsNullOrWhiteSpace(CurrentTaskScopeId.Value)
            ? "task-unknown"
            : CurrentTaskScopeId.Value;
        var normalizedModel = string.IsNullOrWhiteSpace(model) ? "model-unknown" : model.Trim();
        var normalizedLanguage = string.IsNullOrWhiteSpace(targetLanguage) ? "lang-unknown" : targetLanguage.Trim();
        return $"{taskScopeId}|{normalizedModel}|{normalizedLanguage}";
    }

    public static ResponseSizeHealthSnapshot GetSnapshot(string bucketKey)
    {
        lock (Sync)
        {
            var bucket = GetOrCreateBucket_NoLock(bucketKey);
            var sampleCount = bucket.Count;
            var average = sampleCount == 0 ? 0.0 : bucket.Average();
            var isValid = sampleCount > 3;
            var dynamicGuard = isValid ? (int)Math.Ceiling(average * GuardMultiplier) : (int?)null;
            var guard = dynamicGuard.HasValue
                ? Math.Min(dynamicGuard.Value, AbsoluteGuardBytes)
                : AbsoluteGuardBytes;
            return new ResponseSizeHealthSnapshot(sampleCount, average, guard, isValid);
        }
    }

    public static void Record(string bucketKey, int responseBytes, bool qualifiedSample)
    {
        if (!qualifiedSample)
        {
            return;
        }

        lock (Sync)
        {
            var bucket = GetOrCreateBucket_NoLock(bucketKey);
            bucket.Enqueue(Math.Max(0, responseBytes));
            while (bucket.Count > WindowSize)
            {
                bucket.Dequeue();
            }
        }
    }

    private static Queue<int> GetOrCreateBucket_NoLock(string bucketKey)
    {
        var normalized = string.IsNullOrWhiteSpace(bucketKey) ? "task-unknown|model-unknown|lang-unknown" : bucketKey.Trim();
        if (!QualifiedWindowsByBucket.TryGetValue(normalized, out var bucket))
        {
            bucket = new Queue<int>();
            QualifiedWindowsByBucket[normalized] = bucket;
        }

        return bucket;
    }

    private sealed class Scope : IDisposable
    {
        private readonly string? _previous;

        public Scope(string? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            CurrentTaskScopeId.Value = _previous;
        }
    }
}

internal sealed record ResponseSizeHealthSnapshot(
    int SampleCount,
    double AverageBytes,
    int GuardThresholdBytes,
    bool IsValid);

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

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true
    };
}

internal static class RuntimePathPolicy
{
    private const string TempDirEnvKey = "SUBTITLEEXTRACTSLATOR_TEMPDIR";

    public static string GetTempRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable(TempDirEnvKey);
        var root = string.IsNullOrWhiteSpace(fromEnv)
            ? Path.Combine(Path.GetTempPath(), "SubtitleExtractslator")
            : fromEnv.Trim();

        Directory.CreateDirectory(root);
        return root;
    }

    public static string GetIntermediateDirectory(string relativePath)
    {
        var path = Path.Combine(GetTempRoot(), relativePath);
        Directory.CreateDirectory(path);
        return path;
    }
}

internal static class ErrorSnapshotWriter
{
    private static readonly AsyncLocal<string?> TranslateHistoryDirectory = new();

    public static string Write(string prefix, IReadOnlyDictionary<string, string?> sections)
    {
        var dir = RuntimePathPolicy.GetIntermediateDirectory("snapshots");
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

        var historyDir = RuntimePathPolicy.GetIntermediateDirectory("translatehistory");
        Directory.CreateDirectory(historyDir);
        TranslateHistoryDirectory.Value = historyDir;
        return new HistoryScope(current);
    }

    public static string WriteMarkdown(string prefix, IReadOnlyDictionary<string, string?> sections, string? fileTag = null)
    {
        var dir = TranslateHistoryDirectory.Value;
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = RuntimePathPolicy.GetIntermediateDirectory("translatehistory");
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
