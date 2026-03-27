using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SubtitleExtractslator.Cli;

internal sealed class OpenSubtitlesAccessor
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private readonly OpenSubtitlesSettings _settings;
    private string? _token;

    private OpenSubtitlesAccessor(OpenSubtitlesSettings settings)
    {
        _settings = settings;
    }

    public static OpenSubtitlesAccessor? CreateFromEnvironment()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENSUBTITLES_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var endpoint = Environment.GetEnvironmentVariable("OPENSUBTITLES_ENDPOINT")?.Trim();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "https://api.opensubtitles.com/api/v1";
        }

        var userAgent = Environment.GetEnvironmentVariable("OPENSUBTITLES_USER_AGENT")?.Trim();
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            userAgent = "SubtitleExtractslator/0.1";
        }

        var username = Environment.GetEnvironmentVariable("OPENSUBTITLES_USERNAME");
        var password = Environment.GetEnvironmentVariable("OPENSUBTITLES_PASSWORD");

        return new OpenSubtitlesAccessor(new OpenSubtitlesSettings(
            endpoint.TrimEnd('/'),
            apiKey,
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

        var url = BuildSearchUrl(query, targetLanguage);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyHeaders(req, includeAuth: true);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenSubtitles search failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {body}");
        }

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

            var language = ReadString(attributes, "language") ?? targetLanguage;
            var release = ReadString(attributes, "release")
                ?? ReadString(attributes, "feature_details")
                ?? "subtitle";
            var score = ReadDouble(attributes, "ratings")
                ?? ReadDouble(attributes, "download_count")
                ?? 0.0;
            var fileId = ReadFirstFileId(attributes);
            var downloadUrl = ReadString(attributes, "url");

            list.Add(new SubtitleCandidate(
                list.Count + 1,
                language,
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

    public async Task DownloadCandidateToFileAsync(SubtitleCandidate candidate, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("OpenSubtitles output path is empty.");
        }

        await EnsureAuthAsync();

        var link = candidate.DownloadUrl;
        if (string.IsNullOrWhiteSpace(link))
        {
            if (string.IsNullOrWhiteSpace(candidate.FileId))
            {
                throw new InvalidOperationException("OpenSubtitles candidate has neither download URL nor file id.");
            }

            link = await ResolveDownloadLinkAsync(candidate.FileId);
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, link);
        ApplyHeaders(req, includeAuth: false);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        if (!resp.IsSuccessStatusCode)
        {
            var body = bytes.Length == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(bytes);
            throw new InvalidOperationException(
                $"OpenSubtitles download failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {body}");
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

        using var req = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint + "/login")
        {
            Content = JsonContent.Create(new
            {
                username = _settings.Username,
                password = _settings.Password
            })
        };
        ApplyHeaders(req, includeAuth: false);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenSubtitles login failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {body}");
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
        using var req = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint + "/download")
        {
            Content = JsonContent.Create(new { file_id = fileId })
        };
        ApplyHeaders(req, includeAuth: true);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenSubtitles download-link request failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {body}");
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

    private string BuildSearchUrl(string query, string targetLanguage)
    {
        var normalizedQuery = Uri.EscapeDataString(query);
        var normalizedLanguage = Uri.EscapeDataString(targetLanguage);
        return _settings.Endpoint
            + "/subtitles?query=" + normalizedQuery
            + "&languages=" + normalizedLanguage
            + "&order_by=download_count&order_direction=desc";
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
