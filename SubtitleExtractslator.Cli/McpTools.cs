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
    public Task<ProbeResult> Probe(string input, string lang)
        => _orchestrator.ProbeAsync(input, lang);

    [McpServerTool(Name = "opensubtitles_search", Title = "Search OpenSubtitles")]
    public Task<OpenSubtitlesResult> OpenSubtitlesSearch(string input, string lang)
        => _orchestrator.SearchOpenSubtitlesAsync(input, lang);

    [McpServerTool(Name = "extract", Title = "Extract subtitle")]
    public Task<ExtractionResult> Extract(string input, string output, string prefer = "en")
        => _orchestrator.ExtractSubtitleAsync(input, output, prefer);

    [McpServerTool(Name = "run_workflow", Title = "Run full subtitle workflow")]
    public Task<WorkflowResult> RunWorkflow(
        string input,
        string lang,
        string output,
        int? cuesPerGroup = null,
        int? bodySize = null,
        int? llmRetryCount = null,
        string? envOverrides = null)
        => _orchestrator.RunWorkflowAsync(
            input,
            lang,
            output,
            cuesPerGroup,
            bodySize,
            llmRetryCount,
            RuntimeEnvironmentOverrides.Parse(envOverrides));
}
