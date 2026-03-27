using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    public Task<OpenSubtitlesResult> SearchOpenSubtitlesAsync(
        string input,
        string targetLanguage,
        OpenSubtitlesSearchQueries queries,
        OpenSubtitlesCredentials? credentials)
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

        var accessor = OpenSubtitlesAccessor.Create(credentials);
        if (accessor is null)
        {
            CliRuntimeLog.Info("opensubtitles", "No OpenSubtitles API key provided by function parameter. Returning empty candidate list.");
            return Task.FromResult(new OpenSubtitlesResult(input, targetLanguage, new List<SubtitleCandidate>()));
        }

        return SearchOpenSubtitlesWithAccessorAsync(input, targetLanguage, queries, accessor);
    }

    public async Task<string> DownloadOpenSubtitleAsync(SubtitleCandidate candidate, string outputPath, OpenSubtitlesCredentials? credentials)
    {
        if (candidate.Source.Equals("mock-source", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Mock OpenSubtitles candidate cannot be adopted as a real subtitle file.");
        }

        var accessor = OpenSubtitlesAccessor.Create(credentials);
        if (accessor is null)
        {
            throw new InvalidOperationException(
                "OpenSubtitles candidate adoption requires opensubtitlesApiKey parameter (and optionally opensubtitlesUsername/opensubtitlesPassword).");
        }

        CliRuntimeLog.Info("opensubtitles", $"Downloading candidate. fileId={candidate.FileId ?? "n/a"} name={candidate.Name}");
        await accessor.DownloadCandidateToFileAsync(candidate, outputPath);
        CliRuntimeLog.Info("opensubtitles", $"Candidate downloaded to: {outputPath}");
        return outputPath;
    }

    public async Task<OpenSubtitlesDownloadResult> DownloadOpenSubtitleAsync(
        string input,
        string targetLanguage,
        string outputPath,
        int candidateRank,
        string? fileId,
        OpenSubtitlesCredentials? credentials)
    {
        if (candidateRank <= 0)
        {
            throw new InvalidOperationException("OpenSubtitles candidate rank must be greater than 0.");
        }

        EnsureInputPathExists(input);
        var accessor = OpenSubtitlesAccessor.Create(credentials);
        if (accessor is null)
        {
            throw new InvalidOperationException(
            "OpenSubtitles download requires opensubtitlesApiKey parameter (and optionally opensubtitlesUsername/opensubtitlesPassword).");
        }

        SubtitleCandidate candidate;
        string strategy;
        if (!string.IsNullOrWhiteSpace(fileId))
        {
            strategy = "direct-file-id";
            candidate = new SubtitleCandidate(
                1,
                NormalizeLanguageToken(targetLanguage),
                0,
                $"opensubtitles-file-{fileId}",
                "opensubtitles",
                null,
                fileId.Trim());
        }
        else
        {
            strategy = "search-rank";
            var searchQueries = BuildSearchQueriesFromInput(input);
            var search = await SearchOpenSubtitlesWithAccessorAsync(input, targetLanguage, searchQueries, accessor);
            if (search.Candidates.Count == 0)
            {
                throw new InvalidOperationException("OpenSubtitles search returned no candidates to download.");
            }

            candidate = search.Candidates
                .OrderBy(x => x.Rank)
                .Skip(candidateRank - 1)
                .FirstOrDefault()
                ?? throw new InvalidOperationException($"OpenSubtitles candidate rank {candidateRank} is out of range. candidates={search.Candidates.Count}.");
        }

        var downloaded = await DownloadOpenSubtitleAsync(candidate, outputPath, credentials);
        return new OpenSubtitlesDownloadResult(
            input,
            targetLanguage,
            downloaded,
            strategy,
            candidateRank,
            candidate.FileId,
            candidate.Name);
    }

    public async Task<OpenSubtitlesDownloadResult> DownloadOpenSubtitleByFileIdAsync(
        string fileId,
        string outputPath,
        OpenSubtitlesCredentials? credentials)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new InvalidOperationException("OpenSubtitles download requires a non-empty fileId from a previous search result.");
        }

        var accessor = OpenSubtitlesAccessor.Create(credentials);
        if (accessor is null)
        {
            throw new InvalidOperationException(
                "OpenSubtitles download requires opensubtitlesApiKey parameter (and optionally opensubtitlesUsername/opensubtitlesPassword).");
        }

        var normalizedFileId = fileId.Trim();
        var candidate = new SubtitleCandidate(
            1,
            "unknown",
            0,
            $"opensubtitles-file-{normalizedFileId}",
            "opensubtitles",
            null,
            normalizedFileId);

        var downloaded = await DownloadOpenSubtitleAsync(candidate, outputPath, credentials);
        return new OpenSubtitlesDownloadResult(
            string.Empty,
            "unknown",
            downloaded,
            "direct-file-id",
            1,
            normalizedFileId,
            candidate.Name);
    }

    private static async Task<OpenSubtitlesResult> SearchOpenSubtitlesWithAccessorAsync(
        string input,
        string targetLanguage,
        OpenSubtitlesSearchQueries queries,
        OpenSubtitlesAccessor accessor)
    {
        try
        {
            ValidateSearchQueries(queries);
            var primaryQuery = queries.SearchQueryPrimary.Trim();
            var normalizedQuery = queries.SearchQueryNormalized.Trim();

            var primaryTargetCandidates = await SearchCandidatesAsync(accessor, primaryQuery, targetLanguage, "primary-target-language");
            if (primaryTargetCandidates.Count > 0)
            {
                return new OpenSubtitlesResult(input, targetLanguage, primaryTargetCandidates);
            }

            if (string.IsNullOrWhiteSpace(normalizedQuery)
                || normalizedQuery.Equals(primaryQuery, StringComparison.OrdinalIgnoreCase))
            {
                CliRuntimeLog.Info("opensubtitles", "Fallback query is empty or duplicate. Returning empty candidate list.");
                return new OpenSubtitlesResult(input, targetLanguage, new List<SubtitleCandidate>());
            }

            var normalizedTargetCandidates = await SearchCandidatesAsync(accessor, normalizedQuery, targetLanguage, "normalized-target-language");
            if (normalizedTargetCandidates.Count > 0)
            {
                return new OpenSubtitlesResult(input, targetLanguage, normalizedTargetCandidates);
            }

            var primaryAnyLanguageCandidates = await SearchCandidatesAsync(accessor, primaryQuery, null, "primary-any-language");
            if (primaryAnyLanguageCandidates.Count > 0)
            {
                return new OpenSubtitlesResult(input, targetLanguage, primaryAnyLanguageCandidates);
            }

            var normalizedAnyLanguageCandidates = await SearchCandidatesAsync(accessor, normalizedQuery, null, "normalized-any-language");
            return new OpenSubtitlesResult(input, targetLanguage, normalizedAnyLanguageCandidates);
        }
        catch (Exception ex)
        {
            CliRuntimeLog.Warn("opensubtitles", $"Real search failed. Returning empty candidate list. reason={ex.Message}");
            return new OpenSubtitlesResult(input, targetLanguage, new List<SubtitleCandidate>());
        }
    }

    public static OpenSubtitlesSearchQueries BuildSearchQueriesFromInput(string input)
    {
        var primaryQuery = BuildPrimaryOpenSubtitlesQuery(input);
        var normalizedQuery = BuildEpisodeStyleFallbackQuery(input);
        return new OpenSubtitlesSearchQueries(primaryQuery, normalizedQuery);
    }

    private static void ValidateSearchQueries(OpenSubtitlesSearchQueries queries)
    {
        if (string.IsNullOrWhiteSpace(queries.SearchQueryPrimary))
        {
            throw new InvalidOperationException("OpenSubtitles search requires non-empty searchQueryPrimary.");
        }

        if (string.IsNullOrWhiteSpace(queries.SearchQueryNormalized))
        {
            throw new InvalidOperationException("OpenSubtitles search requires non-empty searchQueryNormalized.");
        }
    }

    private static async Task<List<SubtitleCandidate>> SearchCandidatesAsync(
        OpenSubtitlesAccessor accessor,
        string query,
        string? targetLanguage,
        string stage)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            CliRuntimeLog.Info("opensubtitles", $"Search stage={stage} skipped because query is empty.");
            return new List<SubtitleCandidate>();
        }

        var languageLabel = string.IsNullOrWhiteSpace(targetLanguage) ? "any" : targetLanguage;
        CliRuntimeLog.Info("opensubtitles", $"Search stage={stage} query={query} lang={languageLabel}");
        var candidates = await accessor.SearchAsync(query, targetLanguage ?? "und", maxResults: 5);
        CliRuntimeLog.Info("opensubtitles", $"Search stage={stage} completed. candidateCount={candidates.Count}");
        return candidates;
    }

    private static string BuildPrimaryOpenSubtitlesQuery(string input)
        => NormalizeSearchToken(Path.GetFileNameWithoutExtension(input));

    private static string BuildEpisodeStyleFallbackQuery(string input)
    {
        var fileNameToken = NormalizeSearchToken(Path.GetFileNameWithoutExtension(input));
        var parentToken = NormalizeSearchToken(Path.GetFileName(Path.GetDirectoryName(input) ?? string.Empty));

        var seriesOrTitle = fileNameToken;
        if (ContainsEpisodeMarker(fileNameToken) && !string.IsNullOrWhiteSpace(parentToken))
        {
            seriesOrTitle = parentToken;
        }

        if (string.IsNullOrWhiteSpace(seriesOrTitle))
        {
            seriesOrTitle = parentToken;
        }

        if (string.IsNullOrWhiteSpace(seriesOrTitle))
        {
            seriesOrTitle = "unknown";
        }

        var (season, episode) = TryParseSeasonEpisode(input);
        return $"{seriesOrTitle} s{season:00}e{episode:00}";
    }

    private static (int Season, int Episode) TryParseSeasonEpisode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (0, 0);
        }

        var direct = Regex.Match(text, @"\bs(?<season>\d{1,2})[\s._-]*e(?<episode>\d{1,2})\b", RegexOptions.IgnoreCase);
        if (direct.Success
            && int.TryParse(direct.Groups["season"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var season)
            && int.TryParse(direct.Groups["episode"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var episode))
        {
            return (Math.Clamp(season, 0, 99), Math.Clamp(episode, 0, 99));
        }

        var compact = Regex.Match(text, @"\b(?<season>\d{1,2})x(?<episode>\d{1,2})\b", RegexOptions.IgnoreCase);
        if (compact.Success
            && int.TryParse(compact.Groups["season"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out season)
            && int.TryParse(compact.Groups["episode"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out episode))
        {
            return (Math.Clamp(season, 0, 99), Math.Clamp(episode, 0, 99));
        }

        return (0, 0);
    }

    private static bool ContainsEpisodeMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(value, @"\bs\d{1,2}[\s._-]*e\d{1,2}\b", RegexOptions.IgnoreCase)
            || Regex.IsMatch(value, @"\b\d{1,2}x\d{1,2}\b", RegexOptions.IgnoreCase);
    }

    private static string NormalizeSearchToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        normalized = Regex.Replace(normalized, @"[._-]+", " ");
        normalized = Regex.Replace(normalized, @"\bs\d{1,2}[\s._-]*e\d{1,2}\b", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\b\d{1,2}x\d{1,2}\b", string.Empty, RegexOptions.IgnoreCase);
        normalized = Regex.Replace(
            normalized,
            @"\b(480p|576p|720p|1080p|2160p|x264|x265|h\.?264|h\.?265|webrip|web[- ]?dl|bluray|brrip|hdrip|amzn|nf|hmax|ddp\d(\.\d)?|aac\d(\.\d)?)\b",
            string.Empty,
            RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized.ToLowerInvariant();
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
