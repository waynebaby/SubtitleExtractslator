using System.Text.Json;

namespace SubtitleExtractslator.Cli;

internal static class OpenSubtitlesAuthStore
{
    private const string DefaultEndpoint = "https://api.opensubtitles.com/api/v1";
    private const string DefaultUserAgent = "SubtitleExtractslator/0.1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string CachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SubtitleExtractslator",
        "opensubtitles.auth.json");

    public static OpenSubtitlesCredentials Acquire(
        string? endpointOverride,
        string? userAgentOverride)
    {
        var state = ReadRequiredState();
        var endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? state.Endpoint : endpointOverride.Trim();
        var userAgent = string.IsNullOrWhiteSpace(userAgentOverride) ? state.UserAgent : userAgentOverride.Trim();

        if (string.IsNullOrWhiteSpace(state.ApiKey)
            || string.IsNullOrWhiteSpace(state.Username)
            || string.IsNullOrWhiteSpace(state.Password))
        {
            throw OpenSubtitlesAuthException.ReloginRequired(
                "OpenSubtitles sk auth is empty or incomplete.");
        }

        return new OpenSubtitlesCredentials(
            state.ApiKey,
            state.Username,
            state.Password,
            string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint,
            string.IsNullOrWhiteSpace(userAgent) ? DefaultUserAgent : userAgent);
    }

    public static AuthCommandResult Login(
        string apiKey,
        string username,
        string password,
        string? endpoint,
        string? userAgent)
    {
        var normalized = new OpenSubtitlesAuthState(
            apiKey.Trim(),
            username.Trim(),
            password,
            string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim(),
            string.IsNullOrWhiteSpace(userAgent) ? DefaultUserAgent : userAgent.Trim(),
            DateTimeOffset.UtcNow.ToString("O"));

        var dir = Path.GetDirectoryName(CachePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(CachePath, json);

        return new AuthCommandResult(
            "login",
            true,
            "OpenSubtitles auth login saved.",
            true,
            CachePath);
    }

    public static AuthCommandResult Clear()
    {
        if (File.Exists(CachePath))
        {
            File.Delete(CachePath);
        }

        return new AuthCommandResult(
            "clear",
            true,
            "OpenSubtitles auth cache cleared.",
            false,
            CachePath);
    }

    public static AuthCommandResult Status()
    {
        if (!File.Exists(CachePath))
        {
            return new AuthCommandResult(
                "status",
                true,
                "OpenSubtitles auth cache not found.",
                false,
                CachePath);
        }

        try
        {
            var state = ReadRequiredState();
            var hasAuth = !string.IsNullOrWhiteSpace(state.ApiKey)
                && !string.IsNullOrWhiteSpace(state.Username)
                && !string.IsNullOrWhiteSpace(state.Password);

            return new AuthCommandResult(
                "status",
                true,
                hasAuth ? "OpenSubtitles auth cache is ready." : "OpenSubtitles auth cache is incomplete.",
                hasAuth,
                CachePath);
        }
        catch
        {
            return new AuthCommandResult(
                "status",
                true,
                "OpenSubtitles auth cache exists but is invalid. Run subtitle auth login.",
                false,
                CachePath);
        }
    }

    private static OpenSubtitlesAuthState ReadRequiredState()
    {
        if (!File.Exists(CachePath))
        {
            throw OpenSubtitlesAuthException.ReloginRequired(
                "OpenSubtitles sk auth is empty.");
        }

        try
        {
            var json = File.ReadAllText(CachePath);
            var state = JsonSerializer.Deserialize<OpenSubtitlesAuthState>(json);
            if (state is null)
            {
                throw OpenSubtitlesAuthException.ReloginRequired(
                    "OpenSubtitles sk auth cache is invalid.");
            }

            return state;
        }
        catch (OpenSubtitlesAuthException)
        {
            throw;
        }
        catch (Exception)
        {
            throw OpenSubtitlesAuthException.ReloginRequired(
                "OpenSubtitles sk auth cache cannot be parsed.");
        }
    }
}
