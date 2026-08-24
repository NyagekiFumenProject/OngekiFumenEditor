using System.Globalization;
using System.Numerics;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Skia.Drawing.SvgDrawing;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator.ObjectOperationImplement;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects;
using OngekiFumenEditor.Avalonia.Parser.Ogkr;
using OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.Editor;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
using SkiaSharp;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Svg;

public sealed class SvgPrefabTests
{
    private const string RectangleSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"24\" viewBox=\"0 0 32 24\">" +
        "<path d=\"M2 2 L30 2 L30 22 L2 22 Z\" fill=\"none\" stroke=\"#ff0000\" stroke-width=\"2\"/>" +
        "</svg>";

    [Fact]
    public void OngekiFumen_AddRemove_KeepsSvgPrefabOutOfDisplayableRangesWhileFeatureIsDisabled()
    {
        var fumen = new OngekiFumen();
        using var svg = new SvgStringPrefab
        {
            Content = "A",
            TGrid = new TGrid(8, 240),
            XGrid = new XGrid(1, 120)
        };

        fumen.AddObject(svg);

        Assert.Same(svg, Assert.Single(fumen.SvgPrefabs));
        Assert.DoesNotContain(svg, fumen.GetAllDisplayableObjects(new TGrid(8), new TGrid(9)));
        Assert.DoesNotContain(svg, fumen.GetAllDisplayableObjects(new TGrid(9), new TGrid(10)));

        fumen.RemoveObject(svg);

        Assert.Empty(fumen.SvgPrefabs);
        Assert.DoesNotContain(svg, fumen.GetAllDisplayableObjects());
    }

    [Fact]
    public void Copy_ClonesAllCommonAndStringFieldsWithoutSharingMutableValues()
    {
        using var source = CreateConfiguredStringPrefab();
        using var copy = new SvgStringPrefab();

        copy.Copy(source);

        AssertCommonFields(source, copy);
        Assert.Equal(source.Content, copy.Content);
        Assert.Equal(source.FontSize, copy.FontSize);
        Assert.Equal(source.TypefaceName, copy.TypefaceName);
        Assert.Equal(source.ContentFlowDirection, copy.ContentFlowDirection);
        Assert.Equal(source.ContentLineHeight, copy.ContentLineHeight);
        Assert.NotSame(source.TGrid, copy.TGrid);
        Assert.NotSame(source.XGrid, copy.XGrid);
        Assert.NotSame(source.Rotation, copy.Rotation);
        Assert.NotSame(source.OffsetX, copy.OffsetX);
        Assert.NotSame(source.OffsetY, copy.OffsetY);
        Assert.NotSame(source.ColorSimilar, copy.ColorSimilar);
        Assert.NotSame(source.ColorfulLaneBrightness, copy.ColorfulLaneBrightness);
        Assert.NotSame(source.Opacity, copy.Opacity);
        Assert.NotSame(source.Tolerance, copy.Tolerance);

        source.TGrid.Unit = 99;
        source.Rotation.CurrentValue = 90;
        source.Content = "changed";

        Assert.Equal(12.5f, copy.TGrid.Unit);
        Assert.Equal(-42.25f, copy.Rotation.CurrentValue);
        Assert.Equal("迁移<&>测试", copy.Content);
    }

    [AvaloniaFact]
    public async Task NyagekiString_RoundTripsEveryFieldWithInvariantNumbersUnderFrenchCulture()
    {
        using var source = CreateConfiguredStringPrefab();
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var fumen = new OngekiFumen();
            fumen.AddObject(source);

            var bytes = await new DefaultNyagekiFumenFormatter().SerializeAsync(fumen);
            var text = Encoding.UTF8.GetString(bytes);
            var parser = new DefaultNyagekiFumenParser([new SvgPrefabCommandParser()]);
            var reparsed = await parser.DeserializeAsync(new MemoryStream(bytes, writable: false));
            var actual = Assert.IsType<SvgStringPrefab>(Assert.Single(reparsed.SvgPrefabs));

            Assert.Contains("Rotation[-42.25]", text, StringComparison.Ordinal);
            Assert.Contains("Scale[1.75]", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Scale[1,75]", text, StringComparison.Ordinal);
            AssertCommonFields(source, actual);
            Assert.Equal(source.Content, actual.Content);
            Assert.Equal(source.FontSize, actual.FontSize);
            Assert.Equal(source.TypefaceName, actual.TypefaceName);
            Assert.Equal(source.ContentFlowDirection, actual.ContentFlowDirection);
            Assert.Equal(source.ContentLineHeight, actual.ContentLineHeight);
            Assert.Null(actual.Picture);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [AvaloniaFact]
    public async Task OgkrImage_RoundTripsEveryFieldAndPreservesMissingUnicodePath()
    {
        var missingPath = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"音寄-SVG-不存在-{Guid.NewGuid():N}",
            "车道图.svg"));
        using var source = new SvgImageFilePrefab { SvgFile = new LocalSimpleFile(missingPath) };
        ConfigureCommonFields(source);
        var fumen = new OngekiFumen();
        fumen.AddObject(source);

        var bytes = await new DefaultOngekiFumenFormatter().SerializeAsync(fumen);
        var parser = new DefaultOngekiFumenParser(
        [
            new SvgImageFilePrefabCommand(),
            new SvgStringPrefabCommand()
        ]);
        var reparsed = await parser.DeserializeAsync(new MemoryStream(bytes, writable: false));
        var actual = Assert.IsType<SvgImageFilePrefab>(Assert.Single(reparsed.SvgPrefabs));

        AssertCommonFields(source, actual);
        Assert.NotNull(actual.SvgFile);
        Assert.Equal(missingPath, actual.SvgFile.FullPath, ignoreCase: true);
        Assert.False(File.Exists(actual.SvgFile.LocalPath));
        Assert.Null(actual.Picture);
    }

    [AvaloniaFact]
    public void ImageCopy_SharesNonLocalFileWithoutReadingAllBytes()
    {
        var file = new TrackingNonLocalSvgFile(RectangleSvg);
        var source = new SvgImageFilePrefab { SvgFile = file };
        var copy = new SvgImageFilePrefab();
        var sourceDisposed = false;
        try
        {
            copy.Copy(source);

            Assert.Same(source.SvgFile, copy.SvgFile);
            Assert.Equal(0, file.OpenReadCount);
            Assert.Equal(0, file.ReadAllBytesCount);
            Assert.Null(copy.Picture);

            source.Dispose();
            sourceDisposed = true;

            Assert.False(file.IsDisposed);
            Assert.Null(copy.Picture);
        }
        finally
        {
            if (!sourceDisposed)
                source.Dispose();
            copy.Dispose();
        }

        Assert.True(file.IsDisposed);
    }

    [AvaloniaFact]
    public async Task NyagekiImage_NonLocalSimpleFile_DoesNotEmbedSvgContent()
    {
        var file = new TrackingNonLocalSvgFile(RectangleSvg);
        using var source = new SvgImageFilePrefab { SvgFile = file };
        var fumen = new OngekiFumen();
        fumen.AddObject(source);

        var bytes = await new DefaultNyagekiFumenFormatter().SerializeAsync(fumen);
        var text = Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("ContentBase64[", text, StringComparison.Ordinal);
        Assert.Contains(Base64.Encode(file.FullPath), text, StringComparison.Ordinal);
        Assert.Equal(0, file.ReadAllBytesCount);
    }

    [AvaloniaFact]
    public async Task OgkrImage_NonLocalSimpleFile_DoesNotAppendSvgContent()
    {
        var file = new TrackingNonLocalSvgFile(RectangleSvg);
        using var source = new SvgImageFilePrefab { SvgFile = file };
        var fumen = new OngekiFumen();
        fumen.AddObject(source);

        var bytes = await new DefaultOngekiFumenFormatter().SerializeAsync(fumen);
        var lines = Encoding.UTF8.GetString(bytes)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var svgLine = Assert.Single(
            lines,
            x => x.StartsWith(SvgImageFilePrefab.CommandName, StringComparison.Ordinal));

        Assert.Equal(19, svgLine.Split('\t').Length);
        Assert.Equal(0, file.ReadAllBytesCount);
    }

    [Fact]
    public async Task ImageSvg_DoesNotBuildGeometryWhileFeatureIsDisabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ongeki-svg-{Guid.NewGuid():N}.svg");
        await File.WriteAllTextAsync(path, RectangleSvg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            using var svg = new SvgImageFilePrefab
            {
                SvgFile = new LocalSimpleFile(path),
                ShowOriginColor = true
            };

            Assert.Null(svg.Picture);
            Assert.Null(svg.ProcessingBitmap);
            Assert.Empty(svg.GenerateLineSegments());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StringSvg_DoesNotBuildGeometryWhileFeatureIsDisabled()
    {
        using var svg = new SvgStringPrefab
        {
            Content = "Lane",
            TypefaceName = "Tahoma",
            FontSize = 24,
            ContentLineHeight = 28,
            ColorfulLaneColor = ColorIdConst.LaneGreen,
            ShowOriginColor = true
        };

        Assert.Null(svg.Picture);
        Assert.Null(svg.ProcessingBitmap);
        Assert.Empty(svg.GenerateLineSegments());
    }

    [Fact]
    public void PickSimilarLaneColor_PreservesLegacyWeightedDistanceThreshold()
    {
        using var svg = new SvgStringPrefab
        {
            EnableColorfulLaneSimilar = false
        };
        var source = Color.FromRgb(255, 100, 0);

        svg.ColorSimilar.CurrentValue = 150;
        Assert.Null(svg.PickSimilarLaneColor(source));

        svg.ColorSimilar.CurrentValue = 201;
        var laneColor = Assert.IsType<LaneColor>(svg.PickSimilarLaneColor(source));
        Assert.Equal(LaneType.Left, laneColor.LaneType);
        Assert.Equal(ColorIdConst.LaneRed.Color, laneColor.Color);
    }

    [Fact]
    public void SvgLaneColorIds_AreAvailableOnlyInSvgCompatibilityPalette()
    {
        Assert.DoesNotContain(ColorIdConst.LaneRed, ColorIdConst.AllColors);
        Assert.DoesNotContain(ColorIdConst.LaneGreen, ColorIdConst.AllColors);
        Assert.DoesNotContain(ColorIdConst.LaneBlue, ColorIdConst.AllColors);
        Assert.Contains(ColorIdConst.LaneRed, ColorIdConst.SvgPrefabColors);
        Assert.Contains(ColorIdConst.LaneGreen, ColorIdConst.SvgPrefabColors);
        Assert.Contains(ColorIdConst.LaneBlue, ColorIdConst.SvgPrefabColors);
    }

    [AvaloniaFact]
    public async Task GenerateLaneObjects_ReturnsNothingWhileSvgFeatureIsDisabled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ongeki-svg-lane-{Guid.NewGuid():N}.svg");
        await File.WriteAllTextAsync(path, RectangleSvg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            using var svg = new SvgImageFilePrefab
            {
                SvgFile = new LocalSimpleFile(path),
                EnableColorfulLaneSimilar = false,
                ColorSimilar = { CurrentValue = 50 },
                CurveInterpolaterFactory = DefaultCurveInterpolaterFactory.Default,
                TGrid = new TGrid(4)
            };
            var project = new EditorProjectDataModel();
            var editor = new FumenVisualEditorViewModel()
            {
                EditorContext = new EditorContext
                {
                    ProjectData = project,
                    Fumen = new OngekiFumen()
                },
                ViewWidth = 800,
                ViewHeight = 600
            };

            var operation = new SvgPrefabOperationViewModel(svg);
            var generated = operation.GenerateLaneObjects(editor).ToArray();

            Assert.Empty(generated);

            svg.TGrid = new TGrid(0);
            Assert.Empty(operation.GenerateLaneObjects(editor));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public void OperationGenerator_ConstructsTypedViewAndViewModelWithoutViewLocation()
    {
        using var svg = new SvgStringPrefab { Content = "A" };

        var view = Assert.IsType<SvgPrefabOperationView>(new SvgPrefabOperationGenerator().Generate(svg));
        var viewModel = Assert.IsType<SvgPrefabOperationViewModel>(view.DataContext);

        Assert.Same(svg, viewModel.SvgPrefab);
    }

    private static SvgStringPrefab CreateConfiguredStringPrefab()
    {
        var svg = new SvgStringPrefab
        {
            Content = "迁移<&>测试",
            TypefaceName = "Noto Sans CJK JP",
            FontSize = 31.5,
            ContentLineHeight = 37.25,
            ContentFlowDirection = SvgStringPrefab.FlowDirection.BottomToTop
        };
        ConfigureCommonFields(svg);
        return svg;
    }

    private sealed class TrackingNonLocalSvgFile(string svgContent) : ISimpleFile
    {
        private readonly byte[] content = Encoding.UTF8.GetBytes(svgContent);

        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => "picker/embedded.svg";
        public string? LocalPath => null;
        public string FileName => "embedded.svg";
        public long FileLength => content.LongLength;
        public int OpenReadCount { get; private set; }
        public int ReadAllBytesCount { get; private set; }
        public bool IsDisposed { get; private set; }

        public ValueTask<string[]> ReadAllLines()
        {
            ThrowIfDisposed();
            return ValueTask.FromResult(
                svgContent.Split(["\r\n", "\n"], StringSplitOptions.None));
        }

        public ValueTask<byte[]> ReadAllBytes()
        {
            ThrowIfDisposed();
            ReadAllBytesCount++;
            return ValueTask.FromResult(content.ToArray());
        }

        public Task<Stream> OpenRead()
        {
            ThrowIfDisposed();
            OpenReadCount++;
            return Task.FromResult<Stream>(new MemoryStream(content, writable: false));
        }

        public Task<Stream> OpenWrite() => throw new NotSupportedException();

        public void Dispose()
        {
            IsDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }
    }

    private static void ConfigureCommonFields(SvgPrefabBase svg)
    {
        svg.ColorSimilar.CurrentValue = 345.5f;
        svg.Rotation.CurrentValue = -42.25f;
        svg.EnableColorfulLaneSimilar = false;
        svg.OffsetX.CurrentValue = 0.25f;
        svg.OffsetY.CurrentValue = 0.75f;
        svg.ShowOriginColor = true;
        svg.Opacity.CurrentValue = 0.625f;
        svg.ColorfulLaneBrightness.CurrentValue = 2;
        svg.Scale = 1.75f;
        svg.Tolerance.CurrentValue = 0.125f;
        svg.TGrid = new TGrid(12.5f, 345);
        svg.XGrid = new XGrid(-3.25f, 67);
        svg.IsForceColorful = true;
        svg.ColorfulLaneColor = ColorIdConst.LaneBlue;
        svg.CurveInterpolaterFactory = DefaultCurveInterpolaterFactory.Default;
    }

    private static void AssertCommonFields(SvgPrefabBase expected, SvgPrefabBase actual)
    {
        Assert.Equal(expected.ColorSimilar.CurrentValue, actual.ColorSimilar.CurrentValue);
        Assert.Equal(expected.Rotation.CurrentValue, actual.Rotation.CurrentValue);
        Assert.Equal(expected.EnableColorfulLaneSimilar, actual.EnableColorfulLaneSimilar);
        Assert.Equal(expected.OffsetX.CurrentValue, actual.OffsetX.CurrentValue);
        Assert.Equal(expected.OffsetY.CurrentValue, actual.OffsetY.CurrentValue);
        Assert.Equal(expected.ShowOriginColor, actual.ShowOriginColor);
        Assert.Equal(expected.Opacity.CurrentValue, actual.Opacity.CurrentValue);
        Assert.Equal(expected.ColorfulLaneBrightness.CurrentValue, actual.ColorfulLaneBrightness.CurrentValue);
        Assert.Equal(expected.Scale, actual.Scale);
        Assert.Equal(expected.Tolerance.CurrentValue, actual.Tolerance.CurrentValue);
        Assert.Equal(expected.TGrid.Unit, actual.TGrid.Unit);
        Assert.Equal(expected.TGrid.Grid, actual.TGrid.Grid);
        Assert.Equal(expected.XGrid.Unit, actual.XGrid.Unit);
        Assert.Equal(expected.XGrid.Grid, actual.XGrid.Grid);
        Assert.Equal(expected.IsForceColorful, actual.IsForceColorful);
        Assert.Equal(expected.ColorfulLaneColor.Id, actual.ColorfulLaneColor.Id);
        Assert.Equal(expected.CurveInterpolaterFactory.Name, actual.CurveInterpolaterFactory.Name);
    }

    private static int CountNonTransparentPixels(SKBitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha > 0)
                    count++;
            }
        }

        return count;
    }
}
