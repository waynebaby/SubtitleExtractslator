using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace SubtitleExtractslator.Cli;

internal sealed class OpenSubtitlesAccessor
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };
    private const int SearchRateLimitMaxRetries = 20;

    private readonly OpenSubtitlesSettings _settings;
    private string? _token;

    private OpenSubtitlesAccessor(OpenSubtitlesSettings settings)
    {
        _settings = settings;
    }

    public static OpenSubtitlesAccessor? Create(OpenSubtitlesCredentials? credentials)
    {
        if (credentials is null || string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return null;
        }

        var endpoint = credentials.Endpoint?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "https://api.opensubtitles.com/api/v1";
        }

        var userAgent = credentials.UserAgent?.Trim();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = "SubtitleExtractslator/0.1";
        }

        var username = credentials.Username;
        var password = credentials.Password;

        return new OpenSubtitlesAccessor(new OpenSubtitlesSettings(
            endpoint.TrimEnd('/'),
            credentials.ApiKey,
            userAgent,
            username,
            password));
    }

    public async Task<List<SubtitleCandidate>> SearchAsync(string query, string targetLanguage, int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<SubtitleCandidate>();
        }

        await EnsureAuthAsync();

        var queryVariants = BuildQueryVariants(query);
        var normalizedLanguage = NormalizeLanguage(targetLanguage);
        var languageFallbacks = BuildLanguageFallbacks(normalizedLanguage);

        // Try strict language matches first. If empty, retry without language filter.
        var collected = new List<SubtitleCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var queryVariant in queryVariants)
        {
            foreach (var language in languageFallbacks)
            {
                var page = await SearchSingleAsync(queryVariant, language, maxResults);
                AppendUnique(collected, seen, page, maxResults);
                if (collected.Count >= maxResults)
                {
                    return ReRank(collected, maxResults);
                }
            }
        }

        if (collected.Count > 0)
        {
            return ReRank(collected, maxResults);
        }

        foreach (var queryVariant in queryVariants)
        {
            var page = await SearchSingleAsync(queryVariant, null, maxResults);
            AppendUnique(collected, seen, page, maxResults);
            if (collected.Count >= maxResults)
            {
                return ReRank(collected, maxResults);
            }
        }

        return ReRank(collected, maxResults);
    }

    public async Task DownloadCandidateToFileAsync(SubtitleCandidate candidate, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("OpenSubtitles output path is empty.");
        }

        await EnsureAuthAsync();

        string? link = null;
        if (!string.IsNullOrWhiteSpace(candidate.FileId))
        {
            link = await ResolveDownloadLinkAsync(candidate.FileId);
        }
        else if (!string.IsNullOrWhiteSpace(candidate.DownloadUrl))
        {
            // Fallback only when file_id is unavailable.
            link = candidate.DownloadUrl;
        }
        else
        {
            throw new InvalidOperationException("OpenSubtitles candidate has neither file_id nor direct download URL.");
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, link);
        ApplyHeaders(req, includeAuth: false);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        if (!resp.IsSuccessStatusCode)
        {
            var body = bytes.Length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(bytes);
            var debug = BuildHttpDebugBlock("download-file", req, null, resp, body);
            throw new InvalidOperationException(
                $"OpenSubtitles download failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {body}\n{debug}");
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(outputPath, bytes);
    }

    private async Task EnsureAuthAsync()
    {
        if (!string.IsNullOrWhiteSpace(_token))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            return;
        }

        var loginPayload = new
        {
            username = _settings.Username,
            password = _settings.Password
        };
        var loginPayloadText = JsonSerializer.Serialize(loginPayload);
        using var req = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint + "/login")
        {
            Content = JsonContent.Create(loginPayload)
        };
        ApplyHeaders(req, includeAuth: false);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            var debug = BuildHttpDebugBlock("login", req, loginPayloadText, resp, body);
            throw new InvalidOperationException(
                $"OpenSubtitles login failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {body}\n{debug}");
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("token", out var tokenEl)
            && tokenEl.ValueKind == JsonValueKind.String)
        {
            _token = tokenEl.GetString();
        }
    }

    private async Task<string> ResolveDownloadLinkAsync(string fileId)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            throw new InvalidOperationException(
                "OpenSubtitles /download requires Authorization bearer token. "
                + "Provide opensubtitlesUsername and opensubtitlesPassword so login token can be created.");
        }

        var downloadPayload = new { file_id = fileId };
        var downloadPayloadText = JsonSerializer.Serialize(downloadPayload);
        using var req = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint + "/download")
        {
            Content = JsonContent.Create(downloadPayload)
        };
        ApplyHeaders(req, includeAuth: true);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            var debug = BuildHttpDebugBlock("resolve-download-link", req, downloadPayloadText, resp, body);
            throw new InvalidOperationException(
                $"OpenSubtitles download-link request failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {body}\n{debug}");
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("link", out var linkEl)
            && linkEl.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(linkEl.GetString()))
        {
            return linkEl.GetString()!;
        }

        throw new InvalidOperationException("OpenSubtitles response does not contain download link.");
    }

    private async Task<List<SubtitleCandidate>> SearchSingleAsync(string query, string? language, int maxResults)
    {
        var url = BuildSearchUrl(query, language);
        var body = await SendSearchWithRateLimitRetryAsync(url);

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return new List<SubtitleCandidate>();
        }

        var list = new List<SubtitleCandidate>();
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("attributes", out var attributes))
            {
                continue;
            }

            var detectedLanguage = ReadString(attributes, "language") ?? language ?? "und";
            var release = ReadReleaseName(attributes);
            var score = ReadDouble(attributes, "ratings")
                ?? ReadDouble(attributes, "download_count")
                ?? 0.0;
            var fileId = ReadFirstFileId(attributes);
            var downloadUrl = ReadString(attributes, "url");

            list.Add(new SubtitleCandidate(
                list.Count + 1,
                detectedLanguage,
                score,
                release,
                "opensubtitles",
                downloadUrl,
                fileId));

            if (list.Count >= maxResults)
            {
                break;
            }
        }

        return list;
    }

    private async Task<string> SendSearchWithRateLimitRetryAsync(string url)
    {
        for (var attempt = 1; attempt <= SearchRateLimitMaxRetries; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            ApplyHeaders(req, includeAuth: true);

            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            var body = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                return body;
            }

            var isRateLimited = IsRateLimited(resp, body);
            if (!isRateLimited)
            {
                var debug = BuildHttpDebugBlock("search", req, null, resp, body);
                throw new InvalidOperationException(
                    $"OpenSubtitles search failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {body}\n{debug}");
            }

            if (attempt >= SearchRateLimitMaxRetries)
            {
                var debug = BuildHttpDebugBlock("search-rate-limited", req, null, resp, body);
                throw new InvalidOperationException(
                    $"OpenSubtitles search rate-limited after {SearchRateLimitMaxRetries} retries. Last response: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}\n{debug}");
            }

            var delay = ComputeRateLimitDelay(attempt, resp);
            CliRuntimeLog.Warn(
                "opensubtitles",
                $"Search rate-limited. attempt={attempt}/{SearchRateLimitMaxRetries} waitSeconds={delay.TotalSeconds:0} url={url}");
            await Task.Delay(delay);
        }

        throw new InvalidOperationException("OpenSubtitles search failed due to unexpected retry loop termination.");
    }

    private static bool IsRateLimited(HttpResponseMessage response, string body)
    {
        if ((int)response.StatusCode == 429)
        {
            return true;
        }

        return body.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || body.Contains("too many requests", StringComparison.OrdinalIgnoreCase)
            || body.Contains("429", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan ComputeRateLimitDelay(int attempt, HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var retryAfter = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(retryAfter))
            {
                if (int.TryParse(retryAfter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
                {
                    return TimeSpan.FromSeconds(Math.Min(seconds, 120));
                }

                if (DateTimeOffset.TryParse(retryAfter, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when))
                {
                    var delta = when - DateTimeOffset.UtcNow;
                    if (delta > TimeSpan.Zero)
                    {
                        return delta > TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(2) : delta;
                    }
                }
            }
        }

        var fallbackSeconds = Math.Min(60, 2 + (attempt * 2));
        return TimeSpan.FromSeconds(fallbackSeconds);
    }

    private string BuildSearchUrl(string query, string? language)
    {
        var normalizedQuery = Uri.EscapeDataString(query);
        var url = _settings.Endpoint
            + "/subtitles?query=" + normalizedQuery
            + "&order_by=download_count&order_direction=desc";

        if (!string.IsNullOrWhiteSpace(language))
        {
            url += "&languages=" + Uri.EscapeDataString(language);
        }

        return url;
    }

    private static List<string> BuildQueryVariants(string query)
    {
        var list = new List<string>();
        AddDistinct(list, query.Trim());

        var spaced = Regex.Replace(query, "[._-]+", " ").Trim();
        spaced = Regex.Replace(spaced, "\\s+", " ").Trim();
        AddDistinct(list, spaced);

        var stripped = Regex.Replace(
            spaced,
            "\\b(480p|576p|720p|1080p|2160p|x264|x265|h\\.?264|h\\.?265|webrip|web[- ]?dl|bluray|brrip|hdrip|amzn|nf|hmax|ddp\\d(\\.\\d)?|aac\\d(\\.\\d)?)\\b",
            string.Empty,
            RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, "\\s+", " ").Trim();
        AddDistinct(list, stripped);

        return list.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    private static string NormalizeLanguage(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "und";
        }

        return value.Trim().ToLowerInvariant();
    }

    private static List<string> BuildLanguageFallbacks(string language)
    {
        var list = new List<string>();
        AddDistinct(list, language);

        if (language is "zh" or "zh-cn" or "zh-tw")
        {
            AddDistinct(list, "zh");
            AddDistinct(list, "zho");
            AddDistinct(list, "chi");
            AddDistinct(list, "zh-cn");
            AddDistinct(list, "zh-tw");
        }
        else if (language is "en" or "eng")
        {
            AddDistinct(list, "en");
            AddDistinct(list, "eng");
        }

        return list.Where(x => !string.IsNullOrWhiteSpace(x) && !x.Equals("und", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static string ReadReleaseName(JsonElement attributes)
    {
        var release = ReadString(attributes, "release");
        if (!string.IsNullOrWhiteSpace(release))
        {
            return release;
        }

        if (attributes.TryGetProperty("feature_details", out var feature)
            && feature.ValueKind == JsonValueKind.Object)
        {
            var title = ReadString(feature, "title");
            var season = ReadString(feature, "season_number");
            var episode = ReadString(feature, "episode_number");
            var year = ReadString(feature, "year");

            var label = title ?? "subtitle";
            if (!string.IsNullOrWhiteSpace(season) && !string.IsNullOrWhiteSpace(episode))
            {
                label += $" S{season}E{episode}";
            }

            if (!string.IsNullOrWhiteSpace(year))
            {
                label += $" ({year})";
            }

            return label;
        }

        return "subtitle";
    }

    private static List<SubtitleCandidate> ReRank(List<SubtitleCandidate> items, int maxResults)
    {
        return items
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxResults))
            .Select((x, i) => x with { Rank = i + 1 })
            .ToList();
    }

    private static void AppendUnique(
        List<SubtitleCandidate> target,
        HashSet<string> seen,
        IEnumerable<SubtitleCandidate> candidates,
        int maxResults)
    {
        foreach (var candidate in candidates)
        {
            var key = !string.IsNullOrWhiteSpace(candidate.FileId)
                ? $"file:{candidate.FileId}"
                : $"name:{candidate.Name}|lang:{candidate.Language}";

            if (!seen.Add(key))
            {
                continue;
            }

            target.Add(candidate);
            if (target.Count >= maxResults)
            {
                break;
            }
        }
    }

    private static void AddDistinct(List<string> values, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.Trim();
        if (values.Any(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        values.Add(normalized);
    }

    private static string BuildHttpDebugBlock(
        string operation,
        HttpRequestMessage request,
        string? requestBody,
        HttpResponseMessage response,
        string? responseBody)
    {
        var sb = new StringBuilder();
        sb.AppendLine("OPEN_SUB_HTTP_DEBUG_START");
        sb.AppendLine($"operation: {operation}");
        sb.AppendLine($"request.method: {request.Method}");
        sb.AppendLine($"request.url: {request.RequestUri}");
        sb.AppendLine("request.headers:");
        AppendHeaders(sb, request.Headers, request.Content?.Headers);
        sb.AppendLine("request.body:");
        sb.AppendLine(SanitizeBody(requestBody));
        sb.AppendLine($"response.status: {(int)response.StatusCode} {response.ReasonPhrase}");
        sb.AppendLine("response.headers:");
        AppendHeaders(sb, response.Headers, response.Content?.Headers);
        sb.AppendLine("response.body:");
        sb.AppendLine(SanitizeBody(responseBody));
        sb.AppendLine("OPEN_SUB_HTTP_DEBUG_END");
        return sb.ToString();
    }

    private static void AppendHeaders(StringBuilder sb, params HttpHeaders?[] headers)
    {
        foreach (var headerSet in headers)
        {
            if (headerSet is null)
            {
                continue;
            }

            foreach (var header in headerSet)
            {
                var combined = string.Join(", ", header.Value);
                sb.AppendLine($"  {header.Key}: {SanitizeHeaderValue(header.Key, combined)}");
            }
        }
    }

    private static string SanitizeHeaderValue(string key, string value)
    {
        if (key.Equals("Api-Key", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
            || key.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase))
        {
            return "***REDACTED***";
        }

        return value;
    }

    private static string SanitizeBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty)";
        }

        var sanitized = body;
        sanitized = Regex.Replace(sanitized, "(\"password\"\\s*:\\s*\")[^\"]*(\")", "$1***REDACTED***$2", RegexOptions.IgnoreCase);
        sanitized = Regex.Replace(sanitized, "(\"api[_-]?key\"\\s*:\\s*\")[^\"]*(\")", "$1***REDACTED***$2", RegexOptions.IgnoreCase);

        const int maxBodyLength = 8000;
        if (sanitized.Length > maxBodyLength)
        {
            return sanitized[..maxBodyLength] + "\n...(truncated)...";
        }

        return sanitized;
    }

    private void ApplyHeaders(HttpRequestMessage request, bool includeAuth)
    {
        request.Headers.TryAddWithoutValidation("Api-Key", _settings.ApiKey);
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse(_settings.UserAgent));

        if (includeAuth && !string.IsNullOrWhiteSpace(_token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return null;
        }

        return value.ToString();
    }

    private static double? ReadDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var parsed))
        {
            return parsed;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? ReadFirstFileId(JsonElement attributes)
    {
        if (!attributes.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var file in files.EnumerateArray())
        {
            if (file.TryGetProperty("file_id", out var id))
            {
                if (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var asLong))
                {
                    return asLong.ToString(CultureInfo.InvariantCulture);
                }

                if (id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                {
                    return id.GetString();
                }
            }
        }

        return null;
    }

}

internal sealed record OpenSubtitlesSettings(
    string Endpoint,
    string ApiKey,
    string UserAgent,
    string? Username,
    string? Password);
