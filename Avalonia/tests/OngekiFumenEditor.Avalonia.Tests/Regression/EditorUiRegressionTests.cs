using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Modules.ToolBars.Controls;
using Gekimini.Avalonia.Modules.ToolBars.Views;
using Gekimini.Avalonia.UI.ValueConverters;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence;
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
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using AvaloniaImage = Avalonia.Controls.Image;

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
    public void ToolbarOverflowButton_IsHiddenUntilItemsOverflow()
    {
        var toolBarsView = new ToolBarsView();
        var toolBar = new AdaptiveToolBar { Width = 360 };
        toolBar.Items.Add(new Button { Width = 90, Content = "one" });
        toolBar.Items.Add(new Button { Width = 90, Content = "two" });
        toolBar.Items.Add(new Button { Width = 90, Content = "three" });
        toolBarsView.ToolBarTray.ToolBars.Add(toolBar);

        var window = new Window
        {
            Width = 400,
            Height = 80,
            Content = toolBarsView
        };

        try
        {
            window.Show();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            var overflowButton = Assert.Single(
                toolBar.GetVisualDescendants().OfType<ToggleButton>(),
                button => button.Name == "PART_OverflowButton");
            var mainPanelBorder = Assert.Single(
                toolBar.GetVisualDescendants().OfType<Border>(),
                border => border.Name == "MainPanelBorder");

            Assert.False(toolBar.HasOverflowItems);
            Assert.False(overflowButton.IsVisible);
            Assert.Equal(0, mainPanelBorder.Margin.Right);

            toolBar.Width = 100;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.True(toolBar.HasOverflowItems);
            Assert.True(overflowButton.IsVisible);
            Assert.Equal(11, mainPanelBorder.Margin.Right);

            toolBar.Width = 360;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.False(toolBar.HasOverflowItems);
            Assert.False(overflowButton.IsVisible);
            Assert.Equal(0, mainPanelBorder.Margin.Right);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ViewLocator_ReattachedToolbarViewRestoresIconAndCommandBindings()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<Gekimini.Avalonia.Utils.ITypeCollectedActivator<IView>, TestToolbarViewActivator>()
            .BuildServiceProvider();
        var locator = new ViewLocator(
            services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ViewLocator>>(),
            services);
        var viewModel = new TestToolbarItemViewModel();
        var view = Assert.IsType<TestToolbarItemView>(locator.Build(viewModel));
        var host = new ContentControl();
        var window = new Window
        {
            Width = 120,
            Height = 80,
            Content = host
        };

        try
        {
            window.Show();
            host.Content = view;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Same(viewModel, view.DataContext);
            Assert.NotNull(view.Button.Command);
            Assert.NotNull(view.Icon.Source);

            host.Content = null;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Same(viewModel, view.DataContext);

            host.Content = view;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            Assert.Same(viewModel, view.DataContext);
            Assert.NotNull(view.Button.Command);
            Assert.NotNull(view.Icon.Source);
            Assert.True(view.Button.IsEffectivelyEnabled);

            view.Button.Command!.Execute(null);
            Assert.Equal(1, viewModel.ExecuteCount);
        }
        finally
        {
            window.Close();
            services.Dispose();
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

#if DEBUG
    [Fact]
    public async Task DebugPerformanceMonitor_CanReadAndClearWhileRendering()
    {
        var monitor = new DefaultDebugPerfomenceMonitor();
        var drawing = new CommonDrawingBase();
        var drawingTarget = new TestDrawingTarget();

        var renderTask = Task.Run(() =>
        {
            for (var frame = 0; frame < 1000; frame++)
            {
                monitor.OnBeforeRender();
                monitor.OnBeginDrawing(drawing);
                monitor.OnBeginTargetDrawing(drawingTarget);
                monitor.CountDrawCall(drawing);
                monitor.OnAfterDrawing(drawing);
                monitor.OnAfterTargetDrawing(drawingTarget);
                monitor.OnAfterRender();
                monitor.PostUIRenderTime(TimeSpan.FromMilliseconds(1));
            }
        });

        var statisticsTask = Task.Run(() =>
        {
            for (var sample = 0; sample < 1000; sample++)
            {
                var render = monitor.GetRenderPerformenceData();
                Assert.True(render.AveSpendTicks >= 0);

                var builder = new StringBuilder();
                monitor.FormatStatistics(builder);
                monitor.Clear();
            }
        });

        await Task.WhenAll(renderTask, statisticsTask);
    }
#endif

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

    private sealed class TestToolbarItemViewModel : Gekimini.Avalonia.ViewModels.ViewModelBase
    {
        public TestToolbarItemViewModel()
        {
            using var stream = ResourceUtils.OpenReadResourceStream("Icons/home.png");
            IconSource = new AvaloniaBitmap(stream);
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => ExecuteCount++);
        }

        public AvaloniaBitmap IconSource { get; }

        public System.Windows.Input.ICommand Command { get; }

        public int ExecuteCount { get; private set; }
    }

    private sealed class TestToolbarItemView : ViewBase
    {
        public TestToolbarItemView()
        {
            Icon = new AvaloniaImage { Width = 16, Height = 16 };
            Icon.Bind(AvaloniaImage.SourceProperty, new global::Avalonia.Data.Binding(nameof(TestToolbarItemViewModel.IconSource)));

            Button = new Button();
            Button.Bind(Button.CommandProperty, new global::Avalonia.Data.Binding(nameof(TestToolbarItemViewModel.Command)));
            Button.Content = Icon;
            Content = Button;
        }

        public Button Button { get; }

        public AvaloniaImage Icon { get; }
    }

    private sealed class TestToolbarViewActivator : Gekimini.Avalonia.Utils.ITypeCollectedActivator<IView>
    {
        public bool TryCreateInstance(IServiceProvider serviceProvider, string fullName, out IView obj)
        {
            if (fullName == typeof(TestToolbarItemView).FullName)
            {
                obj = new TestToolbarItemView();
                return true;
            }

            obj = default!;
            return false;
        }
    }

#if DEBUG
    private sealed class TestDrawingTarget : IDrawingTarget
    {
        public void Initialize(IRenderManagerImpl impl)
        {
        }
    }
#endif
}
