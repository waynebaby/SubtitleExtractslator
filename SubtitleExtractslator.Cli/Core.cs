using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

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
  SubtitleExtractslator.Cli --mode cli <command> [--key value ...]
  SubtitleExtractslator.Cli --mode mcp

Commands:
  probe --input <mediaFile> --lang <targetLang>
  opensubtitles-search --input <mediaFile> --lang <targetLang>
  extract --input <mediaFile> --out <subtitleFile> [--prefer en]
  run-workflow --input <mediaFile> --lang <targetLang> --output <subtitleFile>

Notes:
  In MCP mode, translation source policy is: sampling first, then external provider fallback.
  In CLI mode, translation source policy is: external provider only.
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
            "run-workflow" => JsonSerializer.Serialize(await orchestrator.RunWorkflowAsync(
                options.Require("input"),
                options.Require("lang"),
                options.Require("output")), JsonOptions.Pretty),
            _ => AppOptions.HelpText
        };
    }

    private static string Require(this AppOptions options, string key)
    {
        if (options.Arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing required argument --{key}");
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

    public async Task<WorkflowResult> RunWorkflowAsync(string input, string targetLanguage, string output)
    {
        var probe = await ProbeAsync(input, targetLanguage);
        if (probe.HasTargetLanguage)
        {
            return new WorkflowResult("completed", "target-track-exists", output, new List<GroupTranslationResult>());
        }

        var openResult = await SearchOpenSubtitlesAsync(input, targetLanguage);
        var accepted = false;
        if (openResult.Candidates.Count > 0)
        {
            if (_translator.ModeContext == ModeContext.Cli)
            {
                accepted = AskUserYesNo("OpenSubtitles has candidates. Use candidate #1? [y/N]: ");
            }
        }

        var subtitlePath = output;
        if (!accepted)
        {
            var extract = await ExtractSubtitleAsync(input, output, "en");
            subtitlePath = extract.OutputPath;
        }

        var cues = await _ops.LoadSrtAsync(subtitlePath);
        var groups = GroupingEngine.Group(cues);
        var historical = "v1: empty";
        var groupResults = new List<GroupTranslationResult>();

        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var summary = RollingKnowledge.BuildSceneSummary(group, historical);
            historical = RollingKnowledge.UpdateHistoricalKnowledge(historical, summary, group);
            var translated = await _translator.TranslateGroupAsync(group, targetLanguage, summary, historical);
            groupResults.Add(translated);
        }

        var merged = groupResults.SelectMany(x => x.Cues).ToList();
        await _ops.SaveSrtAsync(output, merged);

        return new WorkflowResult("completed", accepted ? "opensubtitles-adopted" : "local-extraction", output, groupResults);
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
        if (Path.GetExtension(input).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            var inferred = InferLanguageFromFileName(input);
            var srtTracks = new List<SubtitleTrack> { new(0, 0, inferred, "subtitle-file") };
            var hasTargetFromSrt = inferred.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase);
            return new ProbeResult(input, targetLanguage, hasTargetFromSrt, srtTracks);
        }

        var tracks = await ProbeWithFfprobeAsync(input);
        var hasTarget = tracks.Any(x => x.Language.Equals(targetLanguage, StringComparison.OrdinalIgnoreCase));
        return new ProbeResult(input, targetLanguage, hasTarget, tracks);
    }

    public Task<OpenSubtitlesResult> SearchOpenSubtitlesAsync(string input, string targetLanguage)
    {
        var mock = Environment.GetEnvironmentVariable("OPENSUBTITLES_MOCK");
        if (string.IsNullOrWhiteSpace(mock))
        {
            return Task.FromResult(new OpenSubtitlesResult(input, targetLanguage, new List<SubtitleCandidate>()));
        }

        var candidates = new List<SubtitleCandidate>
        {
            new(1, targetLanguage, 0.92, $"{Path.GetFileNameWithoutExtension(input)}.{targetLanguage}.srt", "mock-source")
        };

        return Task.FromResult(new OpenSubtitlesResult(input, targetLanguage, candidates));
    }

    public async Task<ExtractionResult> ExtractSubtitleAsync(string input, string output, string preferredLanguage)
    {
        if (Path.GetExtension(input).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(input, output, overwrite: true);
            return new ExtractionResult(input, output, preferredLanguage, "copied-input-srt");
        }

        var tracks = await ProbeWithFfprobeAsync(input);
        var selected = SelectBestTrack(tracks, preferredLanguage);

        if (selected is null)
        {
            throw new InvalidOperationException("No subtitle track found in source media.");
        }

        var ffmpeg = ResolveExecutable("ffmpeg");
        var args = $"-y -i \"{input}\" -map 0:s:{selected.SubtitleOrder} \"{output}\"";
        await RunProcessAsync(ffmpeg, args);

        return new ExtractionResult(input, output, selected.Language, "ffmpeg-extract");
    }

    public async Task<List<SubtitleCue>> LoadSrtAsync(string path)
    {
        var content = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return SrtSerializer.Parse(content);
    }

    public async Task SaveSrtAsync(string path, List<SubtitleCue> cues)
    {
        var text = SrtSerializer.Serialize(cues);
        await File.WriteAllTextAsync(path, text, Encoding.UTF8);
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
        var ffprobe = ResolveExecutable("ffprobe");
        var args = $"-v error -select_streams s -show_entries stream=index:stream_tags=language,title -of json \"{input}\"";
        string output;
        try
        {
            output = await RunProcessAsync(ffprobe, args);
        }
        catch
        {
            return new List<SubtitleTrack>();
        }

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
        var ext = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        var full = name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name : name + ext;
        return full;
    }

    private static async Task<string> RunProcessAsync(string fileName, string args)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Process failed ({fileName} {args})\n{stderr}");
        }

        return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
    }
}

internal static class GroupingEngine
{
    public static List<SubtitleGroup> Group(List<SubtitleCue> cues)
    {
        var ordered = cues.OrderBy(x => x.Start).ToList();
        var groups = new List<SubtitleGroup>();
        var stack = new List<SubtitleCue>();
        var previousEnd = TimeSpan.Zero;

        foreach (var cue in ordered)
        {
            var gap = stack.Count > 0 ? cue.Start - previousEnd : TimeSpan.Zero;
            var needNewByGap = stack.Count > 0 && gap >= TimeSpan.FromMinutes(1);
            var needNewBySize = stack.Count >= 100;

            if (needNewByGap || needNewBySize)
            {
                groups.Add(new SubtitleGroup(groups.Count + 1, stack.ToList()));
                stack.Clear();
            }

            stack.Add(cue);
            previousEnd = cue.End;
        }

        if (stack.Count > 0)
        {
            groups.Add(new SubtitleGroup(groups.Count + 1, stack));
        }

        return groups;
    }
}

internal static class RollingKnowledge
{
    public static string BuildSceneSummary(SubtitleGroup group, string historical)
    {
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
        string targetLanguage,
        string sceneSummary,
        string historicalKnowledge)
    {
        var sourceLines = group.Cues.SelectMany(x => x.Lines).ToList();

        IReadOnlyList<string> translated;
        if (ModeContext == ModeContext.Mcp)
        {
            translated = await _samplingProvider.TranslateAsync(sourceLines, targetLanguage, sceneSummary, historicalKnowledge);
            if (translated.Count == 0)
            {
                translated = await _externalProvider.TranslateAsync(sourceLines, targetLanguage, sceneSummary, historicalKnowledge);
            }
        }
        else
        {
            translated = await _externalProvider.TranslateAsync(sourceLines, targetLanguage, sceneSummary, historicalKnowledge);
        }

        var rebuilt = RebuildCues(group.Cues, translated);
        ValidateStructure(group.Cues, rebuilt);
        return new GroupTranslationResult(group.GroupIndex, sceneSummary, historicalKnowledge, rebuilt);
    }

    private static List<SubtitleCue> RebuildCues(List<SubtitleCue> original, IReadOnlyList<string> translatedLines)
    {
        var cursor = 0;
        var rebuilt = new List<SubtitleCue>();

        foreach (var cue in original)
        {
            var lines = new List<string>();
            for (var i = 0; i < cue.Lines.Count; i++)
            {
                if (cursor >= translatedLines.Count)
                {
                    lines.Add(cue.Lines[i]);
                }
                else
                {
                    lines.Add(translatedLines[cursor]);
                }

                cursor++;
            }

            rebuilt.Add(cue with { Lines = lines });
        }

        return rebuilt;
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

            if (original[i].Lines.Count != translated[i].Lines.Count)
            {
                throw new InvalidOperationException($"Translation changed line count at cue index {original[i].Index}.");
            }
        }
    }
}

internal interface ITranslationProvider
{
    Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string sceneSummary,
        string historicalKnowledge);
}

internal sealed class SamplingTranslationProvider : ITranslationProvider
{
    public Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string sceneSummary,
        string historicalKnowledge)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}

internal sealed class ExternalTranslationProvider : ITranslationProvider
{
    public Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> lines,
        string targetLanguage,
        string sceneSummary,
        string historicalKnowledge)
    {
        var mode = Environment.GetEnvironmentVariable("TRANSLATION_MODE") ?? "noop";
        if (!mode.Equals("prefix", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<string>>(lines.ToList());
        }

        var translated = lines.Select(x => $"[{targetLanguage}] {x}").ToList();
        return Task.FromResult<IReadOnlyList<string>>(translated);
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

internal sealed record GroupTranslationResult(int GroupIndex, string SceneSummary, string HistoricalKnowledge, List<SubtitleCue> Cues);

internal sealed record WorkflowResult(string Status, string Branch, string OutputPath, List<GroupTranslationResult> Groups);

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true
    };
}
