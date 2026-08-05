using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.Behaviors;
using OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.Behaviors;
using OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Converters;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.UI;
using OngekiFumenEditor.Avalonia.UI.ValueConverters;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class AxamlSmokeTests
{
    private static readonly Type[] AllViewTypes =
    [
        typeof(global::OngekiFumenEditor.Avalonia.Kernel.SettingPages.Audio.Views.AudioSettingView),
        typeof(global::OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.Views.FumenVisualEditorColorSettingView),
        typeof(global::OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.Views.FumenVisualEditorGlobalSettingView),
        typeof(global::OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Dialogs.ConfigKeyBindingDialog),
        typeof(global::OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Views.KeyBindingSettingView),
        typeof(global::OngekiFumenEditor.Avalonia.Kernel.SettingPages.Logs.Views.LogsSettingView),
        typeof(global::OngekiFumenEditor.Avalonia.Kernel.SettingPages.Program.Views.ProgramSettingView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow.Views.AudioAdjustWindowView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.AudioPlayerToolViewer.Views.AudioPlayerToolViewerView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer.Views.FumenBulletPalleteListViewerView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Views.FumenCheckerListViewerView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenConverter.Views.FumenConverterView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.Views.FumenEditorRenderControlViewerView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Views.FumenEditorSelectingObjectViewerView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenEditorSelectingObjectViewer.Views.SelectionFilterView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.Views.FumenMetaInfoBrowserView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UI.Controls.CommonOperationButton),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.BeamOperationView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.ConnectableObjectOperationView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.Dialog.BrushTGridRangeDialogView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.FumenObjectPropertyBrowserView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.HoldOperationView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.InterpolatableSoflanOperationView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.MultiLanesOperationView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.SvgPrefabOperationView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.WallOperationView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenSoflanGroupListViewer.Views.FumenSoflanGroupListViewerView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenTimeSignatureListViewer.Views.FumenTimeSignatureListViewerView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.BatchModeOverlayView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.Dialogs.EditorProjectSetupDialogView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.FumenVisualEditorView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views.UI.Toast),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.FumenVisualEditorSettings.Views.FumenVisualEditorSettingsView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.SplashScreen.Views.SplashScreenView),
        typeof(global::OngekiFumenEditor.Avalonia.Modules.TGridCalculatorToolViewer.Views.TGridCalculatorToolViewerView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ObjectInspectorView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.BaseValueTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.BoolValueTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.BulletPalleteTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.ColorIdEnumTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.CurveInterpolaterFactoryTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.Dialogs.BulletPalleteSelectDialogView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.EnumValueTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.FileInfoTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.RangeValueTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.TGridTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.WidthIdEnumTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.Views.XGridTypeUIView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Controls.RangeValue),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Dialogs.Views.AboutWindowView),
        typeof(global::OngekiFumenEditor.Avalonia.UI.Dialogs.Views.CommonColorPickerView)
    ];

    public static IEnumerable<object[]> ViewTypes()
    {
        return AllViewTypes.Select(static type => new object[] { type });
    }

    [Fact]
    public void ViewInventory_ContainsAllFiftyOneUniqueParameterlessTypes()
    {
        Assert.Equal(51, AllViewTypes.Length);
        Assert.Equal(AllViewTypes.Length, AllViewTypes.Distinct().Count());
        Assert.All(AllViewTypes, static type =>
        {
            Assert.True(type.IsPublic, $"{type.FullName} must remain public for view location.");
            Assert.False(type.IsAbstract, $"{type.FullName} must be constructible.");
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
            Assert.True(typeof(Control).IsAssignableFrom(type), $"{type.FullName} is not an Avalonia Control.");
        });
    }

    [AvaloniaFact]
    public void ApplicationResources_LoadsRequiredThemesAndConverters()
    {
        var application = Application.Current;
        Assert.IsType<TestApplication>(application);
        Assert.NotEmpty(application!.Styles);

        Assert.IsType<EnumToIntConverter>(GetRequiredResource(application, "EnumToIntConverter"));
        Assert.IsType<BoolToVisibilityConverter>(GetRequiredResource(application, "BoolToVisibilityConverter"));
        Assert.IsAssignableFrom<IMultiValueConverter>(
            Assert.IsType<LocalizeConverter>(GetRequiredResource(application, "LocalizeConverter")));
        Assert.IsAssignableFrom<IBrush>(GetRequiredResource(application, "ContainerBackgroundBrush"));
        Assert.IsAssignableFrom<IBrush>(GetRequiredResource(application, "EditorWindowBackgroundBrush"));
        Assert.IsAssignableFrom<IBrush>(GetRequiredResource(application, "EditorToolWindowForegroundBrush"));
        Assert.IsAssignableFrom<IBrush>(GetRequiredResource(application, "EditorInteractionAccentBrush"));
    }

    [AvaloniaTheory]
    [MemberData(nameof(ViewTypes))]
    public void AllParameterlessViews_ConstructAttachAndCompleteLayout(Type viewType)
    {
        var instance = Activator.CreateInstance(viewType);
        Assert.IsAssignableFrom<Control>(instance);
        var control = (Control)instance!;
        var window = control as Window ?? new Window { Content = control };
        window.Width = 800;
        window.Height = 600;

        try
        {
            window.Show();
            window.UpdateLayout();

            Assert.Same(window, control.GetVisualRoot());
            if (control is Toast)
            {
                Assert.False(control.IsVisible);
                return;
            }

            Assert.True(control.Bounds.Width > 0, $"{viewType.FullName} measured to zero width.");
            Assert.True(control.Bounds.Height > 0, $"{viewType.FullName} measured to zero height.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReorderDataGrids_DisableColumnSortingAndAttachTypedBehavior()
    {
        AssertTypedReorderGrid<FumenEditorRenderControlViewerView, RenderControlRowReorderBehavior>();
        AssertTypedReorderGrid<FumenSoflanGroupListViewerView, SoflanGroupRowReorderBehavior>();
    }

    [AvaloniaFact]
    public void Toast_StartsHiddenAndIgnoresEmptyMessages()
    {
        var toast = new Toast();

        Assert.False(toast.IsVisible);
        Assert.Empty(toast.Message);

        toast.ShowMessage("   ");

        Assert.False(toast.IsVisible);
        Assert.Empty(toast.Message);
    }

    [AvaloniaFact]
    public async Task Toast_NewerMessageOutlivesOlderHideTimer()
    {
        var toast = new Toast();

        toast.ShowMessage("first", showTime: 50);
        await Dispatcher.UIThread.InvokeAsync(static () => { });
        Assert.True(toast.IsVisible);
        Assert.Equal("first", toast.Message);

        await Task.Delay(10);
        toast.ShowMessage("second", showTime: 250);
        await Dispatcher.UIThread.InvokeAsync(static () => { });

        await Task.Delay(80);
        await Dispatcher.UIThread.InvokeAsync(static () => { });
        Assert.True(toast.IsVisible);
        Assert.Equal("second", toast.Message);

        await Task.Delay(220);
        await Dispatcher.UIThread.InvokeAsync(static () => { });
        Assert.False(toast.IsVisible);
        Assert.Empty(toast.Message);
    }

    private static object GetRequiredResource(Application application, string key)
    {
        var found = application.TryGetResource(key, application.ActualThemeVariant, out var value);
        Assert.True(found, $"Application resource '{key}' was not loaded.");
        Assert.NotNull(value);
        return value!;
    }

    private static void AssertTypedReorderGrid<TView, TBehavior>()
        where TView : Control, new()
        where TBehavior : class
    {
        var view = new TView();
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = view
        };

        try
        {
            window.Show();
            window.UpdateLayout();

            var matchingGrids = view
                .GetVisualDescendants()
                .OfType<DataGrid>()
                .Where(static grid => Interaction.GetBehaviors(grid).OfType<TBehavior>().Any())
                .ToArray();

            Assert.Single(matchingGrids);
            Assert.False(matchingGrids[0].CanUserSortColumns);
            Assert.Single(Interaction.GetBehaviors(matchingGrids[0]).OfType<TBehavior>());
        }
        finally
        {
            window.Close();
        }
    }
}
