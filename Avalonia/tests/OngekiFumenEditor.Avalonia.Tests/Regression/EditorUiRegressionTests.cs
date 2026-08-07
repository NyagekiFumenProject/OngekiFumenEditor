using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.UI.ValueConverters;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.BatchModeToggle;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.EditorModeSwitch;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.SplashScreen.Commands.ShowSplashScreen;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ValueConverters;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Regression;

public sealed class EditorUiRegressionTests
{
    [Fact]
    public void EditorGlobalSettingJson_RoundTripsHoldColorsAsArgbNumbers()
    {
        var setting = new EditorGlobalSetting();

        var json = JsonSerializer.Serialize(setting, EditorGlobalSetting.JsonTypeInfo);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(JsonValueKind.Number, root.GetProperty(nameof(EditorGlobalSetting.ColorHoldLeft)).ValueKind);
        Assert.Equal(Color.Red.ToArgb(), root.GetProperty(nameof(EditorGlobalSetting.ColorHoldLeft)).GetInt32());

        var roundTrip = JsonSerializer.Deserialize(json, EditorGlobalSetting.JsonTypeInfo);
        Assert.NotNull(roundTrip);
        Assert.Equal(Color.Red.ToArgb(), roundTrip.ColorHoldLeft.ToArgb());
        Assert.Equal(Color.Lime.ToArgb(), roundTrip.ColorHoldCenter.ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), roundTrip.ColorHoldRight.ToArgb());
        Assert.Equal(Color.FromArgb(136, 3, 152).ToArgb(), roundTrip.ColorHoldWallLeft.ToArgb());
        Assert.Equal(Color.FromArgb(35, 4, 117).ToArgb(), roundTrip.ColorHoldWallRight.ToArgb());
    }

    [Fact]
    public void EditorGlobalSetting_OnDeserialized_RestoresLegacyEmptyHoldColors()
    {
        var setting = new EditorGlobalSetting
        {
            ColorHoldLeft = Color.Empty,
            ColorHoldCenter = Color.Empty,
            ColorHoldRight = Color.Empty,
            ColorHoldWallLeft = Color.Empty,
            ColorHoldWallRight = Color.Empty
        };

        setting.OnDeserialized();

        Assert.Equal(Color.Red.ToArgb(), setting.ColorHoldLeft.ToArgb());
        Assert.Equal(Color.Lime.ToArgb(), setting.ColorHoldCenter.ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), setting.ColorHoldRight.ToArgb());
        Assert.Equal(Color.FromArgb(136, 3, 152).ToArgb(), setting.ColorHoldWallLeft.ToArgb());
        Assert.Equal(Color.FromArgb(35, 4, 117).ToArgb(), setting.ColorHoldWallRight.ToArgb());
    }

    [Fact]
    public void EditorGlobalSettingJson_LegacyEmptyColorObjectsAreRecoveredDuringDeserialization()
    {
        const string json = """
                            {
                              "ColorHoldLeft":{"A":0,"B":0,"G":0,"IsEmpty":true,"R":0},
                              "ColorHoldCenter":{"A":0,"B":0,"G":0,"IsEmpty":true,"R":0}
                            }
                            """;

        var setting = JsonSerializer.Deserialize(json, EditorGlobalSetting.JsonTypeInfo);

        Assert.NotNull(setting);
        Assert.Equal(Color.Red.ToArgb(), setting.ColorHoldLeft.ToArgb());
        Assert.Equal(Color.Lime.ToArgb(), setting.ColorHoldCenter.ToArgb());
    }

    [Fact]
    public void SystemDrawingColorJsonConverter_ReadsLegacyObjectShape()
    {
        const string json = """
                            {"A":192,"R":12,"G":34,"B":56,"IsEmpty":false}
                            """;
        var options = new JsonSerializerOptions();
        options.Converters.Add(new SystemDrawingColorJsonConverter());

        var color = JsonSerializer.Deserialize<Color>(json, options);

        Assert.Equal(Color.FromArgb(192, 12, 34, 56), color);
    }

    [AvaloniaFact]
    public void ToolbarCommandDefinitions_ExposeExistingEmbeddedIcons()
    {
        var commands = new (CommandDefinitionBase Definition, string ResourcePath)[]
        {
            (new BatchModeToggleCommandDefinition(), "Icons/icons8-paint-brush-16.png"),
            (new ShowCurveControlAlwaysCommandDefinition(), "Icons/ease.png"),
            (new EditorModeSwitchCommandDefinition(), "Icons/preview.png"),
            (new ShowSplashScreenCommandDefinition(), "Icons/home.png")
        };

        foreach (var (definition, resourcePath) in commands)
        {
            Assert.Equal(ResourceUtils.GetResourceUri(resourcePath), definition.IconSource);
            using var stream = ResourceUtils.OpenReadResourceStream(resourcePath);
            Assert.True(stream.Length > 0, $"Expected toolbar icon '{resourcePath}' to contain data.");

            var bitmap = UriToBitmapConverter.Instance.Convert(
                definition.IconSource,
                typeof(global::Avalonia.Media.Imaging.Bitmap),
                null!,
                CultureInfo.InvariantCulture);
            Assert.IsAssignableFrom<global::Avalonia.Media.Imaging.Bitmap>(bitmap);
        }
    }

    [AvaloniaFact]
    public void PropertyGeneratorConverter_SupportedProperty_ReturnsGeneratedControl()
    {
        var target = new InspectorTarget();
        var property = typeof(InspectorTarget).GetProperty(nameof(InspectorTarget.Value));
        Assert.NotNull(property);
        var wrapper = new PropertyInfoWrapper(property!, target);
        var converter = new PropertyGeneratorConverter();

        var result = converter.Convert(wrapper, typeof(object), null!, CultureInfo.InvariantCulture);

        Assert.IsAssignableFrom<Control>(result);
    }

    [AvaloniaFact]
    public async Task HitObjects_CanBeQueriedWhileRenderFrameRefreshesThem()
    {
        var editor = new FumenVisualEditorViewModel();
        var objects = Enumerable.Range(0, 256).Select(_ => new Tap()).ToArray();
        using var barrier = new Barrier(2);

        var renderTask = Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var frame = 0; frame < 500; frame++)
            {
                editor.ClearHitObjects();
                foreach (var obj in objects)
                    editor.RegisterSelectableObject(obj, Vector2.Zero, new Vector2(16, 16));

                Thread.Yield();
            }
        });

        barrier.SignalAndWait();
        var queryCount = 0;
        do
        {
            var result = editor.QueryHitObjects(new global::Avalonia.Point(0, 0));
            Assert.True(result.SequenceEqual(result.OrderBy(x => x.Id)));
            queryCount++;
        } while (!renderTask.IsCompleted);

        await renderTask;

        Assert.True(queryCount > 0);
        Assert.Equal(objects.Length, editor.QueryHitObjects(new global::Avalonia.Point(0, 0)).Count);
    }

    [AvaloniaTheory]
    [InlineData(typeof(FumenBulletPalleteListViewerView))]
    [InlineData(typeof(FumenCheckerListViewerView))]
    [InlineData(typeof(FumenEditorRenderControlViewerView))]
    [InlineData(typeof(FumenEditorSelectingObjectViewerView))]
    [InlineData(typeof(FumenSoflanGroupListViewerView))]
    [InlineData(typeof(FumenTimeSignatureListViewerView))]
    public void MigratedDataGrids_PreserveColumnMinimumWidthsAndHorizontalScrolling(Type viewType)
    {
        var view = Assert.IsAssignableFrom<Control>(Activator.CreateInstance(viewType));
        var window = new Window
        {
            Width = 320,
            Height = 240,
            Content = view
        };

        try
        {
            window.Show();
            window.UpdateLayout();

            var dataGrids = view.GetVisualDescendants().OfType<DataGrid>()
                .Concat(view.GetLogicalDescendants().OfType<DataGrid>())
                .Distinct()
                .ToArray();
            Assert.NotEmpty(dataGrids);
            Assert.All(dataGrids, grid =>
            {
                Assert.Equal(ScrollBarVisibility.Auto, grid.HorizontalScrollBarVisibility);
                Assert.All(grid.Columns, column =>
                {
                    Assert.True(column.MinWidth > 20,
                        $"{viewType.Name} column '{column.Header}' retained the compressible default minimum width.");
                    Assert.True(column.ActualWidth >= column.MinWidth,
                        $"{viewType.Name} column '{column.Header}' measured below its configured minimum width.");
                });
            });
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class InspectorTarget
    {
        public int Value { get; set; }
    }
}
