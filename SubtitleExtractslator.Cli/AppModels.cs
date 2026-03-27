namespace SubtitleExtractslator.Cli;

internal enum AppMode
{
    Cli,
    Mcp
}

internal enum ModeContext
{
    Cli,
    Mcp
}

internal sealed record SubtitleCue(int Index, TimeSpan Start, TimeSpan End, List<string> Lines);

internal sealed record SubtitleTrack(int StreamIndex, int SubtitleOrder, string Language, string Title, string CodecName);

internal sealed record ProbeResult(string Input, string TargetLanguage, bool HasTargetLanguage, List<SubtitleTrack> Tracks);

internal sealed record SubtitleCandidate(
    int Rank,
    string Language,
    double Score,
    string Name,
    string Source,
    string? DownloadUrl = null,
    string? FileId = null);

internal sealed record OpenSubtitlesCredentials(
    string ApiKey,
    string? Username,
    string? Password,
    string? Endpoint,
    string? UserAgent);

internal sealed record OpenSubtitlesSearchQueries(
    string SearchQueryPrimary,
    string SearchQueryNormalized);

internal sealed record OpenSubtitlesResult(string Input, string TargetLanguage, List<SubtitleCandidate> Candidates);

internal sealed record OpenSubtitlesDownloadResult(
    string Input,
    string TargetLanguage,
    string OutputPath,
    string Strategy,
    int CandidateRank,
    string? FileId,
    string? CandidateName);

internal sealed record ExtractionResult(
    string Input,
    string OutputPath,
    string SelectedLanguage,
    string Strategy,
    string? ArtifactDirectory = null,
    string? ArtifactManifestPath = null);

internal sealed record BatchWorkflowItemResult(
    string Input,
    string OutputPath,
    bool Success,
    string? Status,
    string? Branch,
    string? Error);

internal sealed record BatchWorkflowResult(
    string InputListPath,
    string TargetLanguage,
    string OutputDir,
    string OutputSuffix,
    int Total,
    int Succeeded,
    int Failed,
    List<BatchWorkflowItemResult> Items);

internal sealed record SubtitleGroup(int GroupIndex, List<SubtitleCue> Cues);

internal sealed record GroupTranslationResult(int GroupIndex, string ParaphraseSummary, string ParaphraseHistory, List<SubtitleCue> Cues);

internal sealed record WorkflowResult(string Status, string Branch, string OutputPath, List<GroupTranslationResult> Groups, string? MuxedOutputPath);
