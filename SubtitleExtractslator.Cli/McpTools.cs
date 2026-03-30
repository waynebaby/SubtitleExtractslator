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

    [McpServerTool(Name = "subtitle_timing_check", Title = "Check subtitle timing match")]
    [Description("Compare media duration and subtitle last cue time. Returns whether absolute difference is less than 10 minutes.")]
    public Task<McpToolResult<SubtitleTimingCheckResult>> SubtitleTimingCheck(
        [Description("Input media file path.")]
        string input,
        [Description("Input subtitle file path (*.srt).")]
        string subtitle)
        => ExecuteWithResultAsync(
            "subtitle_timing_check",
            () => _orchestrator.CheckSubtitleTimingAsync(input, subtitle));

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
        [Description("Optional OpenSubtitles API endpoint. Default: https://api.opensubtitles.com/api/v1")]
        string? opensubtitlesEndpoint = null,
        [Description("Optional User-Agent header value for OpenSubtitles API calls.")]
        string? opensubtitlesUserAgent = null)
        => ExecuteWithResultAsync(
            "opensubtitles_search",
            async () =>
            {
                var result = await _orchestrator.SearchOpenSubtitlesAsync(
                    input,
                    lang,
                    new OpenSubtitlesSearchQueries(searchQueryPrimary, searchQueryNormalized),
                    BuildOpenSubtitlesCredentials(opensubtitlesEndpoint, opensubtitlesUserAgent));

                // Keep MCP payload compact; download tool uses fileId, not transient downloadUrl.
                var compactCandidates = result.Candidates
                    .Select(x => x with { DownloadUrl = null })
                    .ToList();
                return result with { Candidates = compactCandidates };
            });

    [McpServerTool(Name = "opensubtitles_download", Title = "Download OpenSubtitles candidate")]
    [Description("Download subtitle from OpenSubtitles by fileId returned from a prior opensubtitles_search result.")]
    public Task<McpToolResult<OpenSubtitlesDownloadResult>> OpenSubtitlesDownload(
        [Description("OpenSubtitles file_id returned by opensubtitles_search candidate entry.")]
        string fileId,
        [Description("Output subtitle file path.")]
        string output,
        [Description("Optional OpenSubtitles API endpoint. Default: https://api.opensubtitles.com/api/v1")]
        string? opensubtitlesEndpoint = null,
        [Description("Optional User-Agent header value for OpenSubtitles API calls.")]
        string? opensubtitlesUserAgent = null)
        => ExecuteWithResultAsync(
            "opensubtitles_download",
            () => _orchestrator.DownloadOpenSubtitleByFileIdAsync(
                fileId,
                output,
                BuildOpenSubtitlesCredentials(opensubtitlesEndpoint, opensubtitlesUserAgent)));

    [McpServerTool(Name = "extract", Title = "Extract subtitle")]
    [Description("Extract subtitle file from input media using preferred language with deterministic fallback.")]
    public Task<McpToolResult<ExtractionResult>> Extract(
        [Description("Input media file path to extract subtitle from.")]
        string input,
        [Description("Output subtitle file path.")]
        string output,
        [Description("Preferred subtitle language code for extraction, default is en.")]
        string prefer = "en",
        [Description("Injected MCP server instance used for sampling-based bitmap OCR in MCP mode.")]
        McpServer mcpServer = null!)
    {
        if (mcpServer is null)
        {
            const string errorMessage = "MCP server instance injection failed in Extract parameter. Sampling-based bitmap OCR in MCP mode is unavailable.";
            CliRuntimeLog.Error("workflow", errorMessage);
            return Task.FromResult(McpToolResult<ExtractionResult>.Failure(
                "mcp_server_injection_failed",
                errorMessage,
                null));
        }

        return ExecuteWithResultAsync(
            "extract",
            async () =>
            {
                using var samplingScope = McpSamplingRuntimeContext.BeginServerScope(mcpServer);
                var result = await _orchestrator.ExtractSubtitleAsync(input, output, prefer);

                // Keep MCP payload compact; artifact details are internal diagnostics.
                return result with
                {
                    ArtifactDirectory = null,
                    ArtifactManifestPath = null
                };
            });
    }

    [McpServerTool(Name = "ffmpeg_set_bin_dir", Title = "Set FFmpeg bin directory")]
    [Description("Apply FFmpeg bin directory to the current MCP process immediately, and optionally persist it to mcp.json server env.")]
    public Task<McpToolResult<FfmpegPathUpdateResult>> FfmpegSetBinDir(
        [Description("Absolute or relative directory path that contains both ffmpeg and ffprobe executables.")]
        string binDir,
        [Description("Whether to persist FFMPEG_BIN_DIR into mcp.json under servers.<server>.env. Default: true.")]
        bool persistToMcpConfig = true,
        [Description("Optional mcp.json path. Default: <current_working_directory>/.vscode/mcp.json")]
        string? mcpConfigPath = null,
        [Description("Optional MCP server name in config. Default: subtitle-extractslator")]
        string? mcpServerName = null)
        => ExecuteWithResultAsync(
            "ffmpeg_set_bin_dir",
            () => Task.FromResult(_orchestrator.ConfigureFfmpegPath(
                binDir,
                persistToMcpConfig,
                mcpConfigPath,
                mcpServerName)));

    [McpServerTool(Name = "translate", Title = "Translate subtitle")]
    [Description("Translate subtitle content to target language and write final SRT output. This tool performs translation only and does not run probe/search/download/extract/mux orchestration.")]
    public async Task<McpToolResult<WorkflowResult>> Translate(
        [Description("Input subtitle file path (*.srt).")]
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
        [Description("Injected MCP server instance used for official sampling requests. If injection fails, translate returns an error under sampling-only policy.")]
        McpServer mcpServer = null!)
    {
        if (mcpServer is null)
        {
            const string errorMessage = "MCP server instance injection failed in Translate parameter. Under sampling-only policy, external fallback is disabled.";
            CliRuntimeLog.Error("workflow", errorMessage);
            return McpToolResult<WorkflowResult>.Failure(
                "mcp_server_injection_failed",
                errorMessage,
                null);
        }
        else
        {
            CliRuntimeLog.Info("workflow", "MCP server instance injection succeeded for Translate parameter.");
        }

        return await ExecuteWithResultAsync(
            "translate",
            async () =>
            {
                using var samplingScope = McpSamplingRuntimeContext.BeginServerScope(mcpServer);
                var result = await _orchestrator.TranslateAsync(
                    input,
                    lang,
                    output,
                    cuesPerGroup,
                    bodySize,
                    llmRetryCount,
                    null);

                // Keep MCP payload small: translation details are already persisted to output file.
                return result with { Groups = new List<GroupTranslationResult>() };
            });
    }

    private static OpenSubtitlesCredentials BuildOpenSubtitlesCredentials(
        string? endpoint,
        string? userAgent)
        => OpenSubtitlesAuthStore.Acquire(endpoint, userAgent);

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
            if (ex is OpenSubtitlesAuthException authEx)
            {
                return McpToolResult<T>.Failure(
                    authEx.Code,
                    BuildAuthFailureMessage(authEx.Message),
                    snapshotPath,
                    OpenSubtitlesAuthException.GuidanceText);
            }

            return McpToolResult<T>.Failure("tool_execution_failed", ex.Message, snapshotPath);
        }
    }

    private static string BuildAuthFailureMessage(string reason)
        => "OpenSubtitles authentication is required. "
            + "Reason: " + reason;
}

internal sealed record McpToolResult<T>(bool Ok, T? Data, McpToolError? Error)
{
    public static McpToolResult<T> Success(T data) => new(true, data, null);

    public static McpToolResult<T> Failure(string code, string message, string? snapshotPath, string? guidance = null)
        => new(
            false,
            default,
            new McpToolError(
                code,
                message,
                snapshotPath,
                guidance,
                DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
}

internal sealed record McpToolError(string Code, string Message, string? SnapshotPath, string? Guidance, string TimeUtc);
