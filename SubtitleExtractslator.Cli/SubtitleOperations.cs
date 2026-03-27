using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SubtitleExtractslator.Cli;

internal sealed class SubtitleOperations
{
    public async Task<ProbeResult> ProbeTracksAsync(string input, string targetLanguage)
    {
        CliRuntimeLog.Info("probe", $"Start probe. input={input} target={targetLanguage}");
        EnsureInputPathExists(input);
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
        if (!string.IsNullOrWhiteSpace(mock))
        {
            var candidates = new List<SubtitleCandidate>
            {
                new(1, targetLanguage, 0.92, $"{Path.GetFileNameWithoutExtension(input)}.{targetLanguage}.srt", "mock-source")
            };

            CliRuntimeLog.Info("opensubtitles", $"Mock candidate generated. count={candidates.Count}");

            return Task.FromResult(new OpenSubtitlesResult(input, targetLanguage, candidates));
        }

        var accessor = OpenSubtitlesAccessor.CreateFromEnvironment();
        if (accessor is null)
        {
            CliRuntimeLog.Info("opensubtitles", "No OpenSubtitles API key configured. Returning empty candidate list.");
            return Task.FromResult(new OpenSubtitlesResult(input, targetLanguage, new List<SubtitleCandidate>()));
        }

        return SearchOpenSubtitlesWithAccessorAsync(input, targetLanguage, accessor);
    }

    public async Task<string> DownloadOpenSubtitleAsync(SubtitleCandidate candidate, string outputPath)
    {
        if (candidate.Source.Equals("mock-source", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Mock OpenSubtitles candidate cannot be adopted as a real subtitle file.");
        }

        var accessor = OpenSubtitlesAccessor.CreateFromEnvironment();
        if (accessor is null)
        {
            throw new InvalidOperationException(
                "OpenSubtitles candidate adoption requires OPENSUBTITLES_API_KEY (and optionally OPENSUBTITLES_USERNAME/OPENSUBTITLES_PASSWORD).");
        }

        CliRuntimeLog.Info("opensubtitles", $"Downloading candidate. fileId={candidate.FileId ?? "n/a"} name={candidate.Name}");
        await accessor.DownloadCandidateToFileAsync(candidate, outputPath);
        CliRuntimeLog.Info("opensubtitles", $"Candidate downloaded to: {outputPath}");
        return outputPath;
    }

    private static async Task<OpenSubtitlesResult> SearchOpenSubtitlesWithAccessorAsync(
        string input,
        string targetLanguage,
        OpenSubtitlesAccessor accessor)
    {
        try
        {
            var query = Path.GetFileNameWithoutExtension(input);
            var candidates = await accessor.SearchAsync(query, targetLanguage, maxResults: 5);
            CliRuntimeLog.Info("opensubtitles", $"Real search completed. candidateCount={candidates.Count}");
            return new OpenSubtitlesResult(input, targetLanguage, candidates);
        }
        catch (Exception ex)
        {
            CliRuntimeLog.Warn("opensubtitles", $"Real search failed. Returning empty candidate list. reason={ex.Message}");
            return new OpenSubtitlesResult(input, targetLanguage, new List<SubtitleCandidate>());
        }
    }

    public async Task<ExtractionResult> ExtractSubtitleAsync(string input, string output, string preferredLanguage)
    {
        CliRuntimeLog.Info("extract", $"Start extract. input={input} output={output} preferred={preferredLanguage}");
        EnsureInputPathExists(input);
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
        var args = $"-nostdin -y -i {QuoteArg(input)} -map 0:s:{selected.SubtitleOrder} -f srt {QuoteArg(output)}";
        CliRuntimeLog.Info("extract", $"Selected subtitle track order={selected.SubtitleOrder} language={selected.Language}");
        CliRuntimeLog.Info("extract", $"Running ffmpeg: {QuoteArg(ffmpeg)} {args}");
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

    public async Task<string> MuxSubtitleIntoVideoAsync(string inputMedia, string subtitlePath, string outputMedia, string language)
    {
        if (Path.GetExtension(inputMedia).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Mux requires a media input file. Received subtitle file as input.");
        }

        if (!File.Exists(subtitlePath))
        {
            throw new InvalidOperationException($"Subtitle file for mux not found: {subtitlePath}");
        }

        var tracks = await ProbeWithFfprobeAsync(inputMedia);
        var newSubtitleIndex = tracks.Count;
        var normalizedLanguage = NormalizeLanguageToken(language);
        var subtitleCodec = ResolveSubtitleCodecForContainer(outputMedia);
        var ffmpeg = ResolveExecutable("ffmpeg");

        var args = $"-nostdin -y -i {QuoteArg(inputMedia)} -i {QuoteArg(subtitlePath)} -map 0 -map 1:0 -c copy -c:s {subtitleCodec} -metadata:s:s:{newSubtitleIndex} language={normalizedLanguage} -metadata:s:s:{newSubtitleIndex} title={QuoteArg($"AI {normalizedLanguage} subtitle")} {QuoteArg(outputMedia)}";
        CliRuntimeLog.Info("mux", $"Running ffmpeg mux: {QuoteArg(ffmpeg)} {args}");
        await RunProcessAsync(ffmpeg, args);
        return outputMedia;
    }

    private static string ResolveSubtitleCodecForContainer(string outputMedia)
    {
        var ext = Path.GetExtension(outputMedia).ToLowerInvariant();
        return ext is ".mp4" or ".m4v" or ".mov" ? "mov_text" : "srt";
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

    private static string QuoteArg(string value)
    {
        var safe = value.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{safe}\"";
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
        var args = $"-v error -select_streams s -show_entries stream=index:stream_tags=language,title -of json {QuoteArg(input)}";
        CliRuntimeLog.Info("ffprobe", $"Running ffprobe: {QuoteArg(ffprobe)} {args}");
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

    private static void EnsureInputPathExists(string input)
    {
        if (File.Exists(input))
        {
            return;
        }

        var mappedDriveHint = string.Empty;
        var root = Path.GetPathRoot(input);
        if (!string.IsNullOrWhiteSpace(root)
            && root.Length >= 2
            && root[1] == ':'
            && char.ToUpperInvariant(root[0]) != 'C')
        {
            mappedDriveHint = " If this path is a mapped drive (for example Z:), try using the UNC path (\\\\server\\share\\...) or remap the drive in the current user session.";
        }

        throw new InvalidOperationException($"Input file not found: {input}.{mappedDriveHint}");
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
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
            process.StandardInput.Close();
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
