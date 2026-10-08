using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SubtitleExtractslator.Cli;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ProbeResult))]
[JsonSerializable(typeof(SubtitleTimingCheckResult))]
[JsonSerializable(typeof(OpenSubtitlesResult))]
[JsonSerializable(typeof(OpenSubtitlesDownloadResult))]
[JsonSerializable(typeof(AuthCommandResult))]
[JsonSerializable(typeof(ExtractionResult))]
[JsonSerializable(typeof(WorkflowResult))]
[JsonSerializable(typeof(BatchWorkflowResult))]
[JsonSerializable(typeof(OpenSubtitlesAuthState))]
[JsonSerializable(typeof(OpenSubtitlesLoginPayload))]
[JsonSerializable(typeof(OpenSubtitlesDownloadPayload))]
[JsonSerializable(typeof(PgsArtifactManifest))]
[JsonSerializable(typeof(List<SubtitleOperations.PgsTimelineEntry>))]
[JsonSerializable(typeof(JsonObject))]
internal partial class AppJsonContext : JsonSerializerContext
{
}