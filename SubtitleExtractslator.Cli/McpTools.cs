using System.ComponentModel;
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
    public Task<ProbeResult> Probe(
        [Description("Input media file path or subtitle file path to inspect.")]
        string input,
        [Description("Target language code to check for existence, e.g. zh, en, ja.")]
        string lang)
        => _orchestrator.ProbeAsync(input, lang);

    [McpServerTool(Name = "opensubtitles_search", Title = "Search OpenSubtitles")]
    [Description("Search OpenSubtitles candidates for the specified input and target language.")]
    public Task<OpenSubtitlesResult> OpenSubtitlesSearch(
        [Description("Input media file path or subtitle file path used to infer search context.")]
        string input,
        [Description("Target subtitle language code to search for, e.g. zh, en, ja.")]
        string lang)
        => _orchestrator.SearchOpenSubtitlesAsync(input, lang);

    [McpServerTool(Name = "extract", Title = "Extract subtitle")]
    [Description("Extract subtitle file from input media using preferred language with deterministic fallback.")]
    public Task<ExtractionResult> Extract(
        [Description("Input media file path to extract subtitle from.")]
        string input,
        [Description("Output subtitle file path.")]
        string output,
        [Description("Preferred subtitle language code for extraction, default is en.")]
        string prefer = "en")
        => _orchestrator.ExtractSubtitleAsync(input, output, prefer);

    [McpServerTool(Name = "run_workflow", Title = "Run full subtitle workflow")]
    [Description("Run the end-to-end subtitle workflow: probe existing tracks, optionally search OpenSubtitles, extract fallback subtitles, perform grouped context-aware translation, and write final SRT output. In MCP mode, sampling is attempted first when McpServer injection is available; otherwise it explicitly falls back to external translation.")]
    public async Task<WorkflowResult> RunWorkflow(
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
        [Description("Injected MCP server instance used for official sampling requests. If injection fails, workflow logs the reason and falls back to external translation.")]
        McpServer mcpServer = null!)
    {
        if (mcpServer is null)
        {
            CliRuntimeLog.Warn(
                "workflow",
                "MCP server instance injection failed in RunWorkflow parameter. Sampling path will be skipped and external provider fallback will be used.");
        }
        else
        {
            CliRuntimeLog.Info("workflow", "MCP server instance injection succeeded for RunWorkflow parameter.");
        }

        using var samplingScope = McpSamplingRuntimeContext.BeginServerScope(mcpServer);
        return await _orchestrator.RunWorkflowAsync(
            input,
            lang,
            output,
            cuesPerGroup,
            bodySize,
            llmRetryCount,
            null);
    }
}
