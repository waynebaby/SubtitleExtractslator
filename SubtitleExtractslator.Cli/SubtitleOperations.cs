using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;

namespace SubtitleExtractslator.Cli;

internal sealed class SubtitleOperations
{
    private static readonly HttpClient BitmapOcrHttp = new();
    private readonly ModeContext _modeContext;

    public SubtitleOperations(ModeContext modeContext)
    {
        _modeContext = modeContext;
    }

    public async Task<ProbeResult> ProbeTracksAsync(string input, string targetLanguage)
    {
        CliRuntimeLog.Info("probe", $"Start probe. input={input} target={targetLanguage}");
        EnsureInputPathExists(input);
        if (Path.GetExtension(input).Equals(".srt", StringComparison.OrdinalIgnoreCase))
        {
            var inferred = InferLanguageFromFileName(input);
            var srtTracks = new List<SubtitleTrack> { new(0, 0, inferred, "subtitle-file", "srt") };
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
        if (IsBitmapSubtitleCodec(selected.CodecName))
        {
            CliRuntimeLog.Info("extract", $"Selected bitmap subtitle track codec={selected.CodecName}. Enter PGS extraction + OCR branch.");
            return await ExtractBitmapSubtitleWithOcrAsync(input, output, selected, preferredLanguage);
        }

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
        var args = $"-v error -select_streams s -show_entries stream=index,codec_name:stream_tags=language,title -of json {QuoteArg(input)}";
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
            var codecName = item.TryGetProperty("codec_name", out var codecEl)
                ? codecEl.GetString() ?? "unknown"
                : "unknown";

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

            tracks.Add(new SubtitleTrack(index, i, language, title, codecName));
            i++;
        }

        CliRuntimeLog.Info("ffprobe", $"ffprobe parse completed. streamCount={tracks.Count}");

        return tracks;
    }

    private async Task<ExtractionResult> ExtractBitmapSubtitleWithOcrAsync(
        string input,
        string output,
        SubtitleTrack selected,
        string preferredLanguage)
    {
        var ffmpeg = ResolveExecutable("ffmpeg");

        var artifactRoot = RuntimePathPolicy.GetIntermediateDirectory("pgs");
        var jobId = $"{Path.GetFileNameWithoutExtension(input)}.{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.{Guid.NewGuid():N}";
        var jobDir = Path.Combine(artifactRoot, SanitizePathSegment(jobId));
        var pngDir = Path.Combine(jobDir, "png");
        Directory.CreateDirectory(pngDir);

        var supPath = Path.Combine(jobDir, "subtitle.sup");
        var timelinePath = Path.Combine(jobDir, "timeline.json");
        var manifestPath = Path.Combine(jobDir, "manifest.json");

        var extractSupArgs = $"-nostdin -y -i {QuoteArg(input)} -map 0:s:{selected.SubtitleOrder} -c:s copy {QuoteArg(supPath)}";
        CliRuntimeLog.Info("extract", $"Running ffmpeg SUP export: {QuoteArg(ffmpeg)} {extractSupArgs}");
        await RunProcessAsync(ffmpeg, extractSupArgs);

        var decoded = PgsSupDecoder.DecodeToPngFrames(supPath, pngDir);
        var timeline = decoded
            .Select((x, i) => new PgsTimelineEntry(i + 1, x.ImagePath, x.Start, x.End))
            .ToList();

        await File.WriteAllTextAsync(timelinePath, JsonSerializer.Serialize(timeline, JsonOptions.Pretty), Encoding.UTF8);
        var manifest = new
        {
            input,
            output,
            selectedLanguage = selected.Language,
            preferredLanguage,
            subtitleOrder = selected.SubtitleOrder,
            codecName = selected.CodecName,
            supPath,
            pngDirectory = pngDir,
            timelinePath,
            imageCount = decoded.Count,
            cueCount = timeline.Count
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions.Pretty), Encoding.UTF8);

        CliRuntimeLog.Info("extract", $"PGS artifacts ready. sup={supPath} pngCount={decoded.Count} timelineCount={timeline.Count}");

        var cues = await BuildOcrCuesAsync(timeline);
        if (cues.Count == 0)
        {
            throw new InvalidOperationException(
                $"PGS extraction completed but OCR produced no subtitle cues. Artifacts: {jobDir}");
        }

        await SaveSrtAsync(output, cues);
        CliRuntimeLog.Info("extract", $"PGS OCR SRT created. cues={cues.Count} output={output}");

        return new ExtractionResult(
            input,
            output,
            selected.Language,
            "pgs-extract-png-timeline-ocr",
            jobDir,
            manifestPath);
    }

    private async Task<List<SubtitleCue>> BuildOcrCuesAsync(List<PgsTimelineEntry> timeline)
    {
        if (timeline.Count == 0)
        {
            return new List<SubtitleCue>();
        }

        var ocrSettings = ResolveBitmapOcrSettings();
        var maxCues = ResolveMaxPgsOcrCues();
        var source = timeline.Take(maxCues).ToList();
        if (timeline.Count > source.Count)
        {
            CliRuntimeLog.Warn("extract", $"PGS cue count capped for OCR. total={timeline.Count} cap={source.Count}");
        }

        var cues = new List<SubtitleCue>(source.Count);

        foreach (var item in source)
        {
            var text = await RunBitmapOcrAsync(item.ImagePath, ocrSettings, _modeContext);

            var cleaned = NormalizeOcrText(text);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            cues.Add(new SubtitleCue(
                cues.Count + 1,
                item.Start,
                item.End,
                SplitCueLines(cleaned)));
        }

        return cues;
    }

    private static BitmapOcrSettings ResolveBitmapOcrSettings()
    {
        var endpoint = Environment.GetEnvironmentVariable("LLM_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_PGS_OCR_ENDPOINT");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://localhost:1234/v1/chat/completions";
        }

        var model = Environment.GetEnvironmentVariable("LLM_MODEL");
        if (string.IsNullOrWhiteSpace(model))
        {
            model = Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_PGS_OCR_MODEL");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            model = "qwen3.5-9b-uncensored-hauhaucs-aggressive";
        }

        var timeoutRaw = Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_PGS_OCR_TIMEOUT_SECONDS");
        var timeoutSeconds = 120;
        if (!string.IsNullOrWhiteSpace(timeoutRaw)
            && int.TryParse(timeoutRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTimeout)
            && parsedTimeout > 0)
        {
            timeoutSeconds = Math.Clamp(parsedTimeout, 5, 600);
        }

        return new BitmapOcrSettings(endpoint.Trim(), model.Trim(), timeoutSeconds);
    }

    private async Task<string> RunBitmapOcrAsync(string imagePath, BitmapOcrSettings settings, ModeContext modeContext)
    {
        if (modeContext == ModeContext.Mcp)
        {
            return await RunBitmapOcrWithSamplingAsync(imagePath, settings.Model);
        }

        return await RunBitmapOcrWithHttpAsync(imagePath, settings);
    }

    private static async Task<string> RunBitmapOcrWithHttpAsync(string imagePath, BitmapOcrSettings settings)
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var imageDataUri = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        var configuredReasoning = Environment.GetEnvironmentVariable("LLM_REASONING");
        if (!string.IsNullOrWhiteSpace(configuredReasoning))
        {
            CliRuntimeLog.Info(
                "extract",
                $"Bitmap OCR ignores LLM_REASONING={configuredReasoning}. Using fixed fallback: off -> low -> unset.");
        }

        var reasoningModes = new string?[] { "off", "low", null };
        Exception? lastError = null;

        foreach (var reasoningMode in reasoningModes)
        {
            try
            {
                CliRuntimeLog.Info(
                    "extract",
                    $"Bitmap OCR request. model={settings.Model} endpoint={settings.Endpoint} reasoning={(reasoningMode ?? "unset")}");
                var payload = BuildBitmapOcrPayload(settings.Model, imageDataUri, reasoningMode);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                using var response = await BitmapOcrHttp.SendAsync(request, cts.Token);
                var body = await response.Content.ReadAsStringAsync(cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    if (LooksLikeMultimodalNotSupported(body))
                    {
                        throw new InvalidOperationException(
                            "Bitmap OCR requires a multimodal-capable model/endpoint. "
                            + "Set LLM_ENDPOINT to an OpenAI-compatible chat-completions endpoint with image support, "
                            + "and set LLM_MODEL to a vision-capable model.");
                    }

                    throw new InvalidOperationException(
                        $"Bitmap OCR HTTP error. status={(int)response.StatusCode} reasoning={(reasoningMode ?? "unset")} body={TruncateForLog(body, 600)}");
                }

                return ExtractBitmapOcrText(body);
            }
            catch (Exception ex)
            {
                lastError = ex;
                if (reasoningMode is not null)
                {
                    CliRuntimeLog.Warn("extract", $"Bitmap OCR reasoning fallback. mode={reasoningMode} failed: {ex.Message}");
                    continue;
                }

                throw new InvalidOperationException("Bitmap OCR failed after reasoning fallback attempts.", ex);
            }
        }

        throw new InvalidOperationException("Bitmap OCR failed before any request was completed.", lastError);
    }

    private static async Task<string> RunBitmapOcrWithSamplingAsync(string imagePath, string model)
    {
        var server = McpSamplingRuntimeContext.CurrentServer;
        if (server is null)
        {
            throw new InvalidOperationException(
                "MCP bitmap OCR requires sampling server scope, but current MCP server is unavailable.");
        }

        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var imageDataUri = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        var prompt = "Extract subtitle text from this image data URI. Return text only.\n"
            + imageDataUri;

        var request = new CreateMessageRequestParams
        {
            MaxTokens = 512,
            Temperature = 0,
            Messages =
            [
                new SamplingMessage
                {
                    Role = Role.User,
                    Content =
                    [
                        new TextContentBlock { Text = prompt }
                    ]
                }
            ],
            ModelPreferences = string.IsNullOrWhiteSpace(model)
                ? null
                : new ModelPreferences
                {
                    Hints =
                    [
                        new ModelHint { Name = model }
                    ]
                }
        };

        var sampled = await server.SampleAsync(request);
        var sampledText = string.Join(
            "\n",
            sampled.Content
                .OfType<TextContentBlock>()
                .Select(x => x.Text)
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        return sampledText;
    }

    private static string BuildBitmapOcrPayload(string model, string imageDataUri, string? reasoningMode)
    {
        var messageContent = new object[]
        {
            new Dictionary<string, object?>
            {
                ["type"] = "text",
                ["text"] = "Extract subtitle text from this image. Return text only."
            },
            new Dictionary<string, object?>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = imageDataUri
                }
            }
        };

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = 0.0,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = messageContent
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(reasoningMode))
        {
            payload["reasoning"] = reasoningMode;
        }

        return JsonSerializer.Serialize(payload);
    }

    private static string ExtractBitmapOcrText(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var choice = choices[0];
        if (!choice.TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var item in content.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("type", out var type)
                    && type.GetString()?.Equals("text", StringComparison.OrdinalIgnoreCase) == true
                    && item.TryGetProperty("text", out var text))
                {
                    if (builder.Length > 0)
                    {
                        builder.Append('\n');
                    }

                    builder.Append(text.GetString() ?? string.Empty);
                }
            }

            return builder.ToString();
        }

        return string.Empty;
    }

    private static string TruncateForLog(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }

    private static bool LooksLikeMultimodalNotSupported(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        return body.Contains("image_url", StringComparison.OrdinalIgnoreCase)
            || body.Contains("multimodal", StringComparison.OrdinalIgnoreCase)
            || body.Contains("vision", StringComparison.OrdinalIgnoreCase)
            || body.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Unrecognized key", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record BitmapOcrSettings(string Endpoint, string Model, int TimeoutSeconds);

    private static int ResolveMaxPgsOcrCues()
    {
        var raw = Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_PGS_OCR_MAX_CUES");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 160;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            return 160;
        }

        return Math.Clamp(parsed, 1, 2000);
    }

    private static bool IsBitmapSubtitleCodec(string? codecName)
    {
        if (string.IsNullOrWhiteSpace(codecName))
        {
            return false;
        }

        return codecName.Equals("hdmv_pgs_subtitle", StringComparison.OrdinalIgnoreCase)
            || codecName.Equals("dvd_subtitle", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeOcrText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Replace("\r", "\n", StringComparison.Ordinal);
        text = Regex.Replace(text, "\\n{3,}", "\n\n");
        text = Regex.Replace(text, "[ \t]{2,}", " ");
        return text.Trim();
    }

    private static List<string> SplitCueLines(string text)
    {
        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Take(3)
            .DefaultIfEmpty(string.Empty)
            .ToList();
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
        return new string(chars).Trim('-');
    }

    private sealed record PgsTimelineEntry(int Index, string ImagePath, TimeSpan Start, TimeSpan End);

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
