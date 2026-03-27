using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;

namespace SubtitleExtractslator.Cli;

[McpServerToolType]
internal sealed class SubtitleMcpTools
{
    private readonly WorkflowOrchestrator _orchestrator;

    public SubtitleMcpTools(WorkflowOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [McpServerTool(Name = "probe", Title = "Probe subtitle tracks")]
    [Description("Probe subtitle tracks from input media or subtitle file and report available languages/tracks.")]
    public Task<McpToolResult<ProbeResult>> Probe(
        [Description("Input media file path or subtitle file path to inspect.")]
        string input,
        [Description("Target language code to check for existence, e.g. zh, en, ja.")]
        string lang)
        => ExecuteWithResultAsync("probe", () => _orchestrator.ProbeAsync(input, lang));

    [McpServerTool(Name = "opensubtitles_search", Title = "Search OpenSubtitles")]
    [Description("Search OpenSubtitles candidates for the specified input and target language.")]
    public Task<McpToolResult<OpenSubtitlesResult>> OpenSubtitlesSearch(
        [Description("Input media file path or subtitle file path used to infer search context.")]
        string input,
        [Description("Target subtitle language code to search for, e.g. zh, en, ja.")]
        string lang,
        [Description("Primary search query, usually current video title/base filename.")]
        string searchQueryPrimary,
        [Description("Normalized fallback search query, e.g. <series_or_title> s00e00.")]
        string searchQueryNormalized,
        [Description("OpenSubtitles API key used for search requests.")]
        string opensubtitlesApiKey,
        [Description("Optional OpenSubtitles username for authenticated login token.")]
        string? opensubtitlesUsername = null,
        [Description("Optional OpenSubtitles password for authenticated login token.")]
        string? opensubtitlesPassword = null,
        [Description("Optional OpenSubtitles API endpoint. Default: https://api.opensubtitles.com/api/v1")]
        string? opensubtitlesEndpoint = null,
        [Description("Optional User-Agent header value for OpenSubtitles API calls.")]
        string? opensubtitlesUserAgent = null)
        => ExecuteWithResultAsync(
            "opensubtitles_search",
            () => _orchestrator.SearchOpenSubtitlesAsync(
                input,
                lang,
            new OpenSubtitlesSearchQueries(searchQueryPrimary, searchQueryNormalized),
                BuildOpenSubtitlesCredentials(opensubtitlesApiKey, opensubtitlesUsername, opensubtitlesPassword, opensubtitlesEndpoint, opensubtitlesUserAgent, requireApiKey: true)!));

    [McpServerTool(Name = "opensubtitles_download", Title = "Download OpenSubtitles candidate")]
    [Description("Download subtitle from OpenSubtitles by fileId returned from a prior opensubtitles_search result.")]
    public Task<McpToolResult<OpenSubtitlesDownloadResult>> OpenSubtitlesDownload(
        [Description("OpenSubtitles file_id returned by opensubtitles_search candidate entry.")]
        string fileId,
        [Description("Output subtitle file path.")]
        string output,
        [Description("OpenSubtitles API key used for download requests.")]
        string opensubtitlesApiKey,
        [Description("Optional OpenSubtitles username for authenticated login token.")]
        string? opensubtitlesUsername = null,
        [Description("Optional OpenSubtitles password for authenticated login token.")]
        string? opensubtitlesPassword = null,
        [Description("Optional OpenSubtitles API endpoint. Default: https://api.opensubtitles.com/api/v1")]
        string? opensubtitlesEndpoint = null,
        [Description("Optional User-Agent header value for OpenSubtitles API calls.")]
        string? opensubtitlesUserAgent = null)
        => ExecuteWithResultAsync(
            "opensubtitles_download",
            () => _orchestrator.DownloadOpenSubtitleByFileIdAsync(
                fileId,
                output,
                BuildOpenSubtitlesCredentials(opensubtitlesApiKey, opensubtitlesUsername, opensubtitlesPassword, opensubtitlesEndpoint, opensubtitlesUserAgent, requireApiKey: true)!));

    [McpServerTool(Name = "extract", Title = "Extract subtitle")]
    [Description("Extract subtitle file from input media using preferred language with deterministic fallback.")]
    public Task<McpToolResult<ExtractionResult>> Extract(
        [Description("Input media file path to extract subtitle from.")]
        string input,
        [Description("Output subtitle file path.")]
        string output,
        [Description("Preferred subtitle language code for extraction, default is en.")]
        string prefer = "en")
        => ExecuteWithResultAsync("extract", () => _orchestrator.ExtractSubtitleAsync(input, output, prefer));

    [McpServerTool(Name = "run_workflow", Title = "Run full subtitle workflow")]
    [Description("Run the end-to-end subtitle workflow: probe existing tracks, optionally search OpenSubtitles, extract fallback subtitles, perform grouped context-aware translation, and write final SRT output. In MCP mode, translation is sampling-only and any sampling/injection failure returns an error.")]
    public async Task<McpToolResult<WorkflowResult>> RunWorkflow(
        [Description("Input media file path or subtitle file path.")]
        string input,
        [Description("Target subtitle language code, e.g. zh, en, ja.")]
        string lang,
        [Description("Output SRT file path.")]
        string output,
        [Description("Optional override for cues per group. If null, workflow default is used.")]
        int? cuesPerGroup = null,
        [Description("Optional override for body size (number of groups per translation unit). If null, workflow default is used.")]
        int? bodySize = null,
        [Description("Optional override for LLM retry count. If null, environment/default settings are used.")]
        int? llmRetryCount = null,
        [Description("Optional output media path for remuxing generated AI subtitle back into the source video as a new subtitle language track.")]
        string? muxOutput = null,
        [Description("Optional OpenSubtitles API key. If absent, workflow skips OpenSubtitles branch and continues local extraction fallback.")]
        string? opensubtitlesApiKey = null,
        [Description("Optional OpenSubtitles username for authenticated login token.")]
        string? opensubtitlesUsername = null,
        [Description("Optional OpenSubtitles password for authenticated login token.")]
        string? opensubtitlesPassword = null,
        [Description("Optional OpenSubtitles API endpoint. Default: https://api.opensubtitles.com/api/v1")]
        string? opensubtitlesEndpoint = null,
        [Description("Optional User-Agent header value for OpenSubtitles API calls.")]
        string? opensubtitlesUserAgent = null,
        [Description("Injected MCP server instance used for official sampling requests. If injection fails, workflow logs the reason and returns an error under sampling-only policy.")]
        McpServer mcpServer = null!)
    {
        if (mcpServer is null)
        {
            const string errorMessage = "MCP server instance injection failed in RunWorkflow parameter. Under sampling-only policy, external fallback is disabled.";
            CliRuntimeLog.Error("workflow", errorMessage);
            return McpToolResult<WorkflowResult>.Failure(
                "mcp_server_injection_failed",
                errorMessage,
                null);
        }
        else
        {
            CliRuntimeLog.Info("workflow", "MCP server instance injection succeeded for RunWorkflow parameter.");
        }

        return await ExecuteWithResultAsync(
            "run_workflow",
            async () =>
            {
                using var samplingScope = McpSamplingRuntimeContext.BeginServerScope(mcpServer);
                return await _orchestrator.RunWorkflowAsync(
                    input,
                    lang,
                    output,
                    cuesPerGroup,
                    bodySize,
                    llmRetryCount,
                    muxOutput,
                    BuildOpenSubtitlesCredentials(opensubtitlesApiKey, opensubtitlesUsername, opensubtitlesPassword, opensubtitlesEndpoint, opensubtitlesUserAgent, requireApiKey: false),
                    null);
            });
    }

    private static OpenSubtitlesCredentials? BuildOpenSubtitlesCredentials(
        string? apiKey,
        string? username,
        string? password,
        string? endpoint,
        string? userAgent,
        bool requireApiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (requireApiKey)
            {
                throw new InvalidOperationException("Missing required parameter: opensubtitlesApiKey");
            }

            if (string.IsNullOrWhiteSpace(username)
                && string.IsNullOrWhiteSpace(password)
                && string.IsNullOrWhiteSpace(endpoint)
                && string.IsNullOrWhiteSpace(userAgent))
            {
                return null;
            }

            throw new InvalidOperationException("opensubtitlesApiKey is required when any OpenSubtitles credential/config parameter is provided.");
        }

        return new OpenSubtitlesCredentials(apiKey, username, password, endpoint, userAgent);
    }

    private static async Task<McpToolResult<T>> ExecuteWithResultAsync<T>(string toolName, Func<Task<T>> action)
    {
        try
        {
            var data = await action();
            CliRuntimeLog.Info("mcp-tool", $"{toolName} completed. ok=true.");
            return McpToolResult<T>.Success(data);
        }
        catch (Exception ex)
        {
            var snapshotPath = ErrorSnapshotWriter.Write(
                "mcp-tool-error",
                new Dictionary<string, string?>
                {
                    ["tool"] = toolName,
                    ["timeUtc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    ["exception"] = ex.ToString()
                });

            CliRuntimeLog.Error("mcp-tool", $"{toolName} failed. {ex.Message}");
            return McpToolResult<T>.Failure("tool_execution_failed", ex.Message, snapshotPath);
        }
    }
}

internal sealed record McpToolResult<T>(bool Ok, T? Data, McpToolError? Error)
{
    public static McpToolResult<T> Success(T data) => new(true, data, null);

    public static McpToolResult<T> Failure(string code, string message, string? snapshotPath)
        => new(
            false,
            default,
            new McpToolError(
                code,
                message,
                snapshotPath,
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
}

internal sealed record McpToolError(string Code, string Message, string? SnapshotPath, string TimeUtc);
