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
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects;
using OngekiFumenEditor.Avalonia.Parser.Ogkr;
using OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.Editor;
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
    public void OngekiFumen_AddRemoveAndDisplayableRange_TracksSvgPrefab()
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
        Assert.Contains(svg, fumen.GetAllDisplayableObjects(new TGrid(8), new TGrid(9)));
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
            Assert.NotNull(actual.Picture);
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
        using var source = new SvgImageFilePrefab { SvgFile = new FileInfo(missingPath) };
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
        Assert.Equal(missingPath, actual.SvgFile.FullName, ignoreCase: true);
        Assert.False(actual.SvgFile.Exists);
        Assert.Null(actual.Picture);
    }

    [Fact]
    public async Task ImageSvg_ProducesVectorSegmentsAndNonTransparentDrawPixels()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ongeki-svg-{Guid.NewGuid():N}.svg");
        await File.WriteAllTextAsync(path, RectangleSvg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            using var svg = new SvgImageFilePrefab
            {
                SvgFile = new FileInfo(path),
                ShowOriginColor = true
            };

            Assert.NotNull(svg.Picture);
            Assert.NotNull(svg.ProcessingBitmap);
            var segments = svg.GenerateLineSegments();
            Assert.NotEmpty(segments);
            Assert.All(segments, x => Assert.True(x.RelativePoints.Count >= 2));

            using var bitmap = new SKBitmap(new SKImageInfo(96, 96, SKColorType.Rgba8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            DefaultSkiaSvgDrawing.DrawToCanvas(canvas, svg, new Vector2(48, 48));
            canvas.Flush();

            Assert.True(CountNonTransparentPixels(bitmap) > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StringSvg_ProducesPictureBitmapAndVectorTextSegments()
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

        Assert.NotNull(svg.Picture);
        Assert.NotNull(svg.ProcessingBitmap);
        Assert.NotEmpty(svg.GenerateLineSegments());
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
    public async Task GenerateLaneObjects_ConvertsRedSvgPathIntoEditableLeftLane()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ongeki-svg-lane-{Guid.NewGuid():N}.svg");
        await File.WriteAllTextAsync(path, RectangleSvg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        try
        {
            using var svg = new SvgImageFilePrefab
            {
                SvgFile = new FileInfo(path),
                EnableColorfulLaneSimilar = false,
                ColorSimilar = { CurrentValue = 50 },
                CurveInterpolaterFactory = DefaultCurveInterpolaterFactory.Default,
                TGrid = new TGrid(4)
            };
            var editor = new FumenVisualEditorViewModel
            {
                ViewWidth = 800,
                ViewHeight = 600
            };
            await editor.New();

            var operation = new SvgPrefabOperationViewModel(svg);
            var generated = operation.GenerateLaneObjects(editor).ToArray();

            Assert.NotEmpty(generated);
            Assert.All(generated, lane =>
            {
                var leftLane = Assert.IsType<LaneLeftStart>(lane);
                Assert.NotEmpty(leftLane.Children);
                Assert.All(leftLane.Children, child => Assert.IsType<LaneLeftNext>(child));
            });

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
