using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using Xunit;
using Xunit.Sdk;

namespace OngekiFumenEditor.Avalonia.Tests.Corpus;

[Trait("Category", "ExternalCorpus")]
public sealed class ExternalFumenCorpusTests
{
    [AvaloniaFact]
    public async Task ExternalCorpus_RamenChart_ParsesExactSemanticSnapshot()
    {
        var inventory = DiscoverRequiredCorpus();
        var chartPath = RequireSingleNamedFile(inventory.Charts, "ramen.nyageki");
        using var harness = new NyagekiCorpusHarness();

        var fumen = await harness.ParseFileAsync(chartPath);
        var fingerprint = FumenSemanticFingerprint.Capture(fumen);

        Assert.Equal("1.0.0", fingerprint.Version);
        Assert.Equal("MikiraSora", fingerprint.Creator);
        Assert.Equal(178d, fingerprint.FirstBpm);
        Assert.Equal(240d, fingerprint.CommonBpm);
        Assert.Equal(240d, fingerprint.MinimumBpm);
        Assert.Equal(240d, fingerprint.MaximumBpm);
        Assert.Equal(4, fingerprint.MeterNumerator);
        Assert.Equal(4, fingerprint.MeterDenominator);
        Assert.Equal(1920, fingerprint.TResolution);
        Assert.Equal(4096, fingerprint.XResolution);
        Assert.Equal(1920, fingerprint.ClickDefinition);
        Assert.False(fingerprint.Tutorial);
        Assert.Equal(2d, fingerprint.BeamDamage);
        Assert.Equal(2d, fingerprint.HardBulletDamage);
        Assert.Equal(4d, fingerprint.DangerBulletDamage);
        Assert.Equal(1d, fingerprint.BulletDamage);
        Assert.Equal(240f, fingerprint.ProgJudgeBpm);

        Assert.Equal(4, fingerprint.BulletPalettes);
        Assert.Equal("A0,A1,A4,A5", fingerprint.BulletPaletteIds);
        Assert.Equal(1, fingerprint.Bpms);
        Assert.Equal(392, fingerprint.Lanes);
        Assert.Equal(392, fingerprint.UniqueLaneRecordIds);
        Assert.Equal(14, fingerprint.MinimumLaneRecordId);
        Assert.Equal(602, fingerprint.MaximumLaneRecordId);
        Assert.Equal(1456, fingerprint.LaneChildren);
        Assert.Equal(191, fingerprint.CurveSegments);
        Assert.Equal(220, fingerprint.CurvePathControls);
        Assert.Equal(0, fingerprint.Beams);
        Assert.Equal(0, fingerprint.BeamChildren);
        Assert.Equal(109, fingerprint.Bells);
        Assert.Equal(0, fingerprint.BellsWithPaletteReference);
        Assert.Equal(88, fingerprint.Bullets);
        Assert.Equal(88, fingerprint.BulletsWithPaletteReference);
        Assert.Equal(74, fingerprint.Flicks);
        Assert.Equal(0, fingerprint.ClickSEs);
        Assert.Equal(12, fingerprint.MeterChanges);
        Assert.Equal(0, fingerprint.Comments);
        Assert.Equal(1, fingerprint.SvgPrefabs);
        Assert.Equal(2, fingerprint.EnemySets);
        Assert.Equal(11, fingerprint.Soflans);
        Assert.Equal(0, fingerprint.IndividualSoflans);
        Assert.Equal(12, fingerprint.LaneBlocks);
        Assert.Equal(532, fingerprint.Taps);
        Assert.Equal(532, fingerprint.TapsWithLaneReference);
        Assert.Equal(148, fingerprint.Holds);
        Assert.Equal(148, fingerprint.HoldsWithLaneReference);
        Assert.Equal(148, fingerprint.HoldsWithEnd);

        AssertRamenSvgPrefab(fumen);
    }

    [Fact]
    public void ExternalCorpus_RamenChart_RecognizesEveryCommandExactlyOnce()
    {
        var inventory = DiscoverRequiredCorpus();
        var chartPath = RequireSingleNamedFile(inventory.Charts, "ramen.nyageki");
        using var harness = new NyagekiCorpusHarness();
        var commandCounts = NyagekiCommandInventory.ReadCommandCounts(chartPath);
        var duplicateParsers = harness.CommandParsers
            .GroupBy(x => x.CommandName, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() != 1)
            .Select(x => x.Key)
            .ToArray();
        var recognizedCommands = harness.CommandParsers
            .Select(x => x.CommandName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownCommands = commandCounts
            .Where(x => !recognizedCommands.Contains(x.Key))
            .ToArray();

        Assert.Empty(duplicateParsers);
        Assert.Equal(29, commandCounts.Count);
        Assert.Empty(unknownCommands);
        Assert.Equal(1, commandCounts["SvgPrefab"]);
        Assert.Single(harness.CommandParsers, x =>
            string.Equals(x.CommandName, "SvgPrefab", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public async Task ExternalCorpus_RamenChart_ParseFormatReparse_PreservesSupportedSemanticFingerprint()
    {
        var inventory = DiscoverRequiredCorpus();
        var chartPath = RequireSingleNamedFile(inventory.Charts, "ramen.nyageki");
        using var harness = new NyagekiCorpusHarness();
        var original = await harness.ParseFileAsync(chartPath);
        var originalFingerprint = FumenSemanticFingerprint.Capture(original);

        var normalizedBytes = await harness.Formatter.SerializeAsync(original);
        var normalizedText = Encoding.UTF8.GetString(normalizedBytes);
        var reparsed = await harness.ParseBytesAsync(normalizedBytes);
        var reparsedFingerprint = FumenSemanticFingerprint.Capture(reparsed);
        var secondNormalizedBytes = await harness.Formatter.SerializeAsync(reparsed);

        Assert.NotEmpty(normalizedBytes);
        AssertRamenSvgPrefab(original);
        AssertRamenSvgPrefab(reparsed);
        Assert.Equal(
            "SvgPrefab\t:\tType[[SVG_STR]], ColorSimilar[600], Rotation[0], EnableColorfulLaneSimilar[True], OffsetX[0.5], OffsetY[0.5], ShowOriginColor[False], Opacity[1], Brightness[0], Scale[8], Tolerance[20], T[64,1560], X[0,0], IsForceColorful[False], ColorfulLaneColorId[1021], CurveInterpolaterFactory[XGrid.Unit limited], Content[44G+], FontSize[32], TypefaceName[Tahoma], FontColorId[1021], ContentFlowDirection[LeftToRight], ContentLineHeight[16]",
            normalizedText.ReplaceLineEndings("\n")
                .Split('\n')
                .Single(x => x.StartsWith("SvgPrefab\t:", StringComparison.Ordinal)));
        Assert.Equal(originalFingerprint, reparsedFingerprint);
        Assert.Equal(
            GetOrderIndependentSerializedLines(normalizedText),
            GetOrderIndependentSerializedLines(Encoding.UTF8.GetString(secondNormalizedBytes)));
    }

    [Fact]
    public async Task ExternalCorpus_RamenProject_ParsesExactMetadataAndResolvesRelativeReferencesReadOnly()
    {
        var inventory = DiscoverRequiredCorpus();
        var projectPath = RequireSingleNamedFile(inventory.Projects, "ramen.nyagekiProj");

        // Product model setters schedule global settings saves; inspect the project metadata without invoking them.
        await using var stream = OpenReadOnly(projectPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var project = document.RootElement;

        Assert.Equal("0.5.4", project.GetProperty("Version").GetString());
        Assert.Equal(Guid.Parse("10838fa9-2edd-4ef6-b260-c71d2b08588d"), project.GetProperty("Id").GetGuid());
        Assert.Equal(1_367_655_416L, project.GetProperty("AudioDuration").GetProperty("Ticks").GetInt64());
        Assert.Equal(713_244_382L, project.GetProperty("RememberLastDisplayTime").GetProperty("Ticks").GetInt64());

        var editorSetting = project.GetProperty("EditorSetting");
        Assert.True(editorSetting.GetProperty("ForceMagneticDock").GetBoolean());
        Assert.Equal(1, editorSetting.GetProperty("BeatSplit").GetInt32());
        Assert.Equal(6d, editorSetting.GetProperty("XGridUnitSpace").GetDouble());
        Assert.Equal(0.75d, editorSetting.GetProperty("VerticalDisplayScale").GetDouble());
        Assert.Equal(0, editorSetting.GetProperty("DisplayTimeFormat").GetInt32());

        var paletteIds = project.GetProperty("StoreBulletPalleteEditorDatas")
            .EnumerateObject()
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "A0", "A1", "A2", "A3", "A4", "A5" }, paletteIds);

        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var audioRelativePath = Assert.IsType<string>(project.GetProperty("AudioFilePath").GetString());
        var chartRelativePath = Assert.IsType<string>(project.GetProperty("FumenFilePath").GetString());
        var audioPath = Path.GetFullPath(Path.Combine(projectDirectory, audioRelativePath));
        var chartPath = Path.GetFullPath(Path.Combine(projectDirectory, chartRelativePath));

        Assert.Equal("track.wav", audioRelativePath);
        Assert.Equal("ramen.nyageki", chartRelativePath);
        Assert.True(File.Exists(audioPath), $"Project audio reference is missing: {audioPath}");
        Assert.True(File.Exists(chartPath), $"Project chart reference is missing: {chartPath}");
        Assert.Equal(audioPath, Assert.Single(inventory.AudioFiles), ignoreCase: true);
        Assert.Equal(chartPath, Assert.Single(inventory.Charts), ignoreCase: true);
        Assert.Equal("bg.png", Path.GetFileName(Assert.Single(inventory.Images)), ignoreCase: true);
        Assert.Empty(inventory.OtherFiles);
    }

    [Fact]
    public async Task ExternalCorpus_NyagekiScripts_AreReadOnlyDiscoveriesAndNeverChartInputs()
    {
        var rootPath = RequireCorpusRoot();
        var inventory = CorpusInventory.Discover(rootPath);
        var relativeScriptPaths = inventory.Scripts
            .Select(x => Path.GetRelativePath(rootPath, x).Replace('\\', '/'))
            .ToArray();

        Assert.Equal(
            new[]
            {
                "scripts/copyLane.nyagekiScript",
                "scripts/copyTiming.nyagekiScript",
                "scripts/replaceLane.nyagekiScript",
                "未命名 2.nyagekiScript"
            },
            relativeScriptPaths);
        Assert.All(inventory.Scripts, path => Assert.EndsWith(".nyagekiScript", path, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(inventory.Charts, path =>
            path.EndsWith(".nyagekiScript", StringComparison.OrdinalIgnoreCase));

        foreach (var scriptPath in inventory.Scripts)
        {
            await using var stream = OpenReadOnly(scriptPath);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var scriptText = await reader.ReadToEndAsync();

            Assert.NotEmpty(scriptText);
            Assert.Contains("ScriptArgs.TargetEditor", scriptText, StringComparison.Ordinal);
        }
    }

    private static CorpusInventory DiscoverRequiredCorpus()
    {
        var inventory = CorpusInventory.Discover(RequireCorpusRoot());
        Assert.Single(inventory.Charts);
        Assert.Single(inventory.Projects);
        return inventory;
    }

    private static string RequireCorpusRoot()
    {
        var location = CorpusLocator.Locate();
        if (location.IsAvailable)
            return location.CandidatePath;

        if (location.IsExplicitOverride)
            throw new XunitException(location.Diagnostic);

        throw SkipException.ForSkip(location.Diagnostic);
    }

    private static string RequireSingleNamedFile(IEnumerable<string> paths, string expectedFileName) =>
        Assert.Single(paths, path =>
            string.Equals(Path.GetFileName(path), expectedFileName, StringComparison.OrdinalIgnoreCase));

    private static FileStream OpenReadOnly(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete,
        4096,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static void AssertRamenSvgPrefab(OngekiFumenEditor.Avalonia.Base.OngekiFumen fumen)
    {
        var svg = Assert.IsType<SvgStringPrefab>(Assert.Single(fumen.SvgPrefabs));

        Assert.Equal("[SVG_STR]", svg.IDShortName);
        Assert.Equal(600f, svg.ColorSimilar.CurrentValue);
        Assert.Equal(0f, svg.Rotation.CurrentValue);
        Assert.True(svg.EnableColorfulLaneSimilar);
        Assert.Equal(0.5f, svg.OffsetX.CurrentValue);
        Assert.Equal(0.5f, svg.OffsetY.CurrentValue);
        Assert.False(svg.ShowOriginColor);
        Assert.Equal(1f, svg.Opacity.CurrentValue);
        Assert.Equal(0f, svg.ColorfulLaneBrightness.CurrentValue);
        Assert.Equal(8f, svg.Scale);
        Assert.Equal(20f, svg.Tolerance.CurrentValue);
        Assert.Equal(64f, svg.TGrid.Unit);
        Assert.Equal(1560, svg.TGrid.Grid);
        Assert.Equal(0f, svg.XGrid.Unit);
        Assert.Equal(0, svg.XGrid.Grid);
        Assert.False(svg.IsForceColorful);
        Assert.Equal(1021, svg.ColorfulLaneColor.Id);
        Assert.Equal("XGrid.Unit limited", svg.CurveInterpolaterFactory.Name);
        Assert.Equal("ま", svg.Content);
        Assert.Equal(32d, svg.FontSize);
        Assert.Equal("Tahoma", svg.TypefaceName);
        Assert.Equal(SvgStringPrefab.FlowDirection.LeftToRight, svg.ContentFlowDirection);
        Assert.Equal(16d, svg.ContentLineHeight);
        Assert.NotNull(svg.Picture);
    }

    private static string[] GetOrderIndependentSerializedLines(string text) =>
        text.ReplaceLineEndings("\n")
            .Split('\n')
            .OrderBy(static line => line, StringComparer.Ordinal)
            .ToArray();
}
