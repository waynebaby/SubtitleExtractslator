using SubtitleExtractslator.Cli;
using Xunit;

namespace SubtitleExtractslator.Tests;

public sealed class TranslationBatchTests
{
    public static IEnumerable<object[]> InputSubtitleFiles()
    {
        var inputDir = GetInputDirectory();
        foreach (var path in Directory.EnumerateFiles(inputDir, "*.srt", SearchOption.TopDirectoryOnly).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            yield return new object[] { path };
        }
    }

    [Fact]
    public void InputFilesDirectory_ShouldContainSrtFiles()
    {
        var inputDir = GetInputDirectory();
        var files = Directory.EnumerateFiles(inputDir, "*.srt", SearchOption.TopDirectoryOnly).ToList();
        Assert.NotEmpty(files);
    }

    [Theory]
    [MemberData(nameof(InputSubtitleFiles))]
    public async Task EachInputFile_ShouldTranslateToChinese_AndWriteToOutputFiles(string inputPath)
    {
        Environment.SetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_TRANSLATION_PARALLELISM", "4");
        Environment.SetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_DUMP_PROMPT", "1");

        var outputDir = GetOutputDirectory();
        Directory.CreateDirectory(outputDir);

        var inputContent = await File.ReadAllTextAsync(inputPath);
        var sourceCues = SrtSerializer.Parse(inputContent);
        Assert.NotEmpty(sourceCues);

        var groups = GroupingEngine.Group(sourceCues);
        Assert.NotEmpty(groups);

        var useRealProvider = string.Equals(
            Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_TEST_USE_REAL_LLM"),
            "1",
            StringComparison.Ordinal);

        ITranslationProvider externalProvider = useRealProvider
            ? new ExternalTranslationProvider()
            : new FakeTranslationProvider();

        ITranslationProvider samplingProvider = useRealProvider
            ? new SamplingTranslationProvider()
            : new FakeTranslationProvider();

        var translator = new TranslationPipeline(
            ModeContext.Cli,
            externalProvider,
            samplingProvider);

        var translatedGroups = new List<GroupTranslationResult>();

        foreach (var unit in BuildTranslationUnits(groups, bodySize: 3))
        {
            var mainGroup = new SubtitleGroup(
                unit.MainGroupIndex,
                unit.BodyGroups.SelectMany(x => x.Cues).ToList());

            var translated = await translator.TranslateGroupAsync(mainGroup, unit.ContextGroups, "zh");
            translatedGroups.Add(translated);
        }

        var translatedCues = translatedGroups
            .SelectMany(x => x.Cues)
            .OrderBy(x => x.Index)
            .ToList();

        Assert.Equal(sourceCues.Count, translatedCues.Count);
        for (var i = 0; i < sourceCues.Count; i++)
        {
            Assert.Equal(sourceCues[i].Index, translatedCues[i].Index);
            Assert.Equal(sourceCues[i].Start, translatedCues[i].Start);
            Assert.Equal(sourceCues[i].End, translatedCues[i].End);
            Assert.All(translatedCues[i].Lines, line => Assert.False(line.StartsWith("中文:", StringComparison.Ordinal)));
            Assert.All(translatedCues[i].Lines, line => Assert.True(ComputeDisplayWidth(line) <= 32));
        }

        Assert.Contains(
            translatedCues.SelectMany(x => x.Lines),
            line => line.Any(IsChineseChar));

        var outputPath = Path.Combine(
            outputDir,
            $"{Path.GetFileNameWithoutExtension(inputPath)}.zh.srt");

        var outputContent = SrtSerializer.Serialize(translatedCues);
        await File.WriteAllTextAsync(outputPath, outputContent);
        Assert.True(File.Exists(outputPath));
    }

    private static string GetInputDirectory()
    {
        var repoRoot = GetRepositoryRoot();
        var candidates = new[]
        {
            Path.Combine(repoRoot, "TestSubtitles", "InputFiles"),
            Path.Combine(repoRoot, "samples")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate)
                && Directory.EnumerateFiles(candidate, "*.srt", SearchOption.TopDirectoryOnly).Any())
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"No input subtitle directory with .srt files found. Checked: {string.Join(", ", candidates)}");
    }

    private static string GetOutputDirectory()
    {
        var outputDir = Path.Combine(GetRepositoryRoot(), "artifacts", "test-output", "translation-batch");
        Directory.CreateDirectory(outputDir);
        return outputDir;
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var marker = Path.Combine(current.FullName, "SubtitleExtractslator.sln");
            if (File.Exists(marker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Cannot find repository root containing SubtitleExtractslator.sln.");
    }

    private static bool IsChineseChar(char c)
    {
        return c >= '\u4E00' && c <= '\u9FFF';
    }

    private static List<TranslationUnit> BuildTranslationUnits(List<SubtitleGroup> groups, int bodySize)
    {
        var units = new List<TranslationUnit>();
        if (groups.Count == 0)
        {
            return units;
        }

        for (var i = 0; i < groups.Count; i += bodySize)
        {
            var bodyGroups = groups.Skip(i).Take(bodySize).ToList();
            var contextGroups = new List<SubtitleGroup>();

            if (i - 1 >= 0)
            {
                contextGroups.Add(groups[i - 1]);
            }

            contextGroups.AddRange(bodyGroups);

            if (i + bodySize < groups.Count)
            {
                contextGroups.Add(groups[i + bodySize]);
            }

            units.Add(new TranslationUnit(
                bodyGroups[0].GroupIndex,
                bodyGroups,
                contextGroups));
        }

        return units;
    }

    private sealed record TranslationUnit(
        int MainGroupIndex,
        List<SubtitleGroup> BodyGroups,
        List<SubtitleGroup> ContextGroups);

    private static int ComputeDisplayWidth(string text)
    {
        var sum = 0;
        foreach (var c in text)
        {
            sum += IsWideChar(c) ? 2 : 1;
        }

        return sum;
    }

    private static bool IsWideChar(char c)
    {
        return (c >= '\u1100' && c <= '\u115F')
            || (c >= '\u2E80' && c <= '\uA4CF')
            || (c >= '\uAC00' && c <= '\uD7A3')
            || (c >= '\uF900' && c <= '\uFAFF')
            || (c >= '\uFE10' && c <= '\uFE19')
            || (c >= '\uFE30' && c <= '\uFE6F')
            || (c >= '\uFF01' && c <= '\uFF60')
            || (c >= '\uFFE0' && c <= '\uFFE6');
    }

    private sealed class FakeTranslationProvider : ITranslationProvider
    {
        public Task<IReadOnlyList<string>> TranslateIndexedAsync(
            IReadOnlyList<string> lines,
            string targetLanguage,
            string contextParaphrase,
            string contextHint)
        {
            var output = lines
                .Select((line, i) => $"测试{i + 1} {line}")
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(output);
        }

        public Task<IReadOnlyList<string>> TranslateAsync(
            IReadOnlyList<string> lines,
            string targetLanguage,
            string paraphraseSummary,
            string previousCycleParaphrase,
            string paraphraseHistory)
        {
            return TranslateIndexedAsync(lines, targetLanguage, paraphraseSummary, "legacy");
        }
    }
}
