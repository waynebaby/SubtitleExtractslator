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

internal sealed record OpenSubtitlesAuthState(
    string ApiKey,
    string Username,
    string Password,
    string? Endpoint,
    string? UserAgent,
    string UpdatedUtc);

internal sealed record AuthCommandResult(
    string Action,
    bool Ok,
    string Message,
    bool HasAuth,
    string CachePath);

internal sealed class OpenSubtitlesAuthException : InvalidOperationException
{
    public const string GuidanceText = "Run subtitle auth login and retry.";

    public OpenSubtitlesAuthException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }

    public static OpenSubtitlesAuthException ReloginRequired(string reason)
        => new(
            "auth_relogin_required",
            $"{reason} Authentication missing, expired, or unauthorized. Please {GuidanceText}");
}

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

internal sealed record SubtitleTimingCheckResult(
    string Input,
    string Subtitle,
    int SubtitleCueCount,
    double VideoDurationSeconds,
    double SubtitleLastCueSeconds,
    double AbsoluteDifferenceSeconds,
    double ThresholdSeconds,
    bool IsWithinThreshold,
    string Verdict);

internal sealed record ExtractionResult(
    string Input,
    string OutputPath,
    string SelectedLanguage,
    string Strategy,
    string? ArtifactDirectory = null,
    string? ArtifactManifestPath = null);

internal sealed record McpConfigUpdateResult(
    string ConfigPath,
    string ServerName,
    string FfmpegBinDir,
    string EnvKey);

internal sealed record FfmpegPathUpdateResult(
    string FfmpegBinDir,
    bool AppliedToCurrentProcess,
    bool PersistedToMcpConfig,
    McpConfigUpdateResult? McpConfigUpdate);

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
