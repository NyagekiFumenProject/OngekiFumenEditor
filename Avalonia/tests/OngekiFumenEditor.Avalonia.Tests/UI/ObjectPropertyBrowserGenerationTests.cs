using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class ObjectPropertyBrowserGenerationTests
{
    private readonly ITestOutputHelper output;

    public ObjectPropertyBrowserGenerationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [AvaloniaFact]
    public void TypeUIGenerators_AreRegistered()
    {
        var generators = IoC.GetAll<ITypeUIGenerator>().ToArray();
        foreach (var generator in generators)
            output.WriteLine($"{generator.GetType().Name}: {string.Join(", ", generator.SupportTypes.Select(t => t.Name))}");

        Assert.NotEmpty(generators);
    }

    [AvaloniaFact]
    public void RefreshSelected_SingleTap_ProducesPropertyWrappers()
    {
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();
        var tap = new Tap();

        browser.RefreshSelected(null, tap);

        var vm = Assert.IsType<global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.FumenObjectPropertyBrowserViewModel>(browser);
        foreach (var wrapper in vm.PropertyInfoWrappers)
            output.WriteLine($"wrapper: {wrapper.DisplayPropertyName} ({wrapper.PropertyInfo?.PropertyType.Name})");

        Assert.NotEmpty(vm.PropertyInfoWrappers);
    }

    [AvaloniaFact]
    public void GenerateUI_ForEachTapPropertyWrapper_ReturnsBoundView()
    {
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();
        var tap = new Tap();

        browser.RefreshSelected(null, tap);

        var vm = Assert.IsType<global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.FumenObjectPropertyBrowserViewModel>(browser);
        Assert.NotEmpty(vm.PropertyInfoWrappers);

        var failures = new List<string>();
        foreach (var wrapper in vm.PropertyInfoWrappers)
        {
            var control = PropertiesUIGenerator.GenerateUI(wrapper);
            output.WriteLine($"{wrapper.DisplayPropertyName}: {(control is null ? "NULL" : control.GetType().Name)}");
            if (control is not IView ||
                control.DataContext is not CommonUIViewModelBase propertyViewModel ||
                !ReferenceEquals(wrapper, propertyViewModel.PropertyInfo))
                failures.Add(wrapper.DisplayPropertyName);
        }

        Assert.Empty(failures);
    }

    private void DumpTree(Visual visual, int depth)
    {
        if (depth > 12)
            return;
        output.WriteLine($"{new string(' ', depth * 2)}{visual.GetType().Name} bounds={visual.Bounds} visible={visual.IsVisible}");
        foreach (var child in visual.GetVisualChildren())
            if (child is Visual v)
                DumpTree(v, depth + 1);
    }

    [AvaloniaFact]
    public void OperationGenerators_ForLaneStart_ReturnControl()
    {
        var lane = new LaneLeftStart();
        var control = OngekiObjectOperationGenerator.GenerateUI(lane);
        output.WriteLine($"LaneStart operation view: {(control is null ? "NULL" : control.GetType().Name)}");
        Assert.NotNull(control);
    }

    [AvaloniaFact]
    public void BrowserView_RendersLaneNextOperationControl_AfterRefreshSelected()
    {
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();
        browser.RefreshSelected((global::OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel)null!);

        var laneStart = new LaneLeftStart();
        var laneNext = new LaneLeftNext();
        laneStart.AddChildObject(laneNext);

        var view = new global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.FumenObjectPropertyBrowserView
        {
            DataContext = browser
        };
        var window = new Window
        {
            Width = 400,
            Height = 800,
            Content = view
        };

        try
        {
            window.Show();

            browser.RefreshSelected(null, laneNext);
            window.UpdateLayout();

            Assert.Same(laneNext, Assert.Single(browser.SelectedObjects));

            var operationView = view.GetVisualDescendants()
                .OfType<global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.ConnectableObjectOperationView>()
                .SingleOrDefault();
            Assert.NotNull(operationView);

            var operationViewModel = Assert.IsType<global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.LaneOperationViewModel>(operationView.DataContext);
            Assert.Same(laneNext, operationViewModel.ConnectableObject);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BrowserView_RendersGeneratedControls_AfterRefreshSelected()
    {
        var browser = IoC.Get<IFumenObjectPropertyBrowser>();
        var view = new global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.Views.FumenObjectPropertyBrowserView
        {
            DataContext = browser
        };
        var window = new Window
        {
            Width = 400,
            Height = 800,
            Content = view
        };

        try
        {
            window.Show();

            browser.RefreshSelected(null, new Tap());
            window.UpdateLayout();

            var itemsControl = view.GetVisualDescendants().OfType<ItemsControl>().FirstOrDefault();
            Assert.NotNull(itemsControl);
            output.WriteLine($"ItemsControl.ItemCount = {itemsControl.ItemCount}");

            var realizedContainers = itemsControl.GetRealizedContainers().ToArray();
            output.WriteLine($"realized containers = {realizedContainers.Length}");
            output.WriteLine($"ItemsControl bounds = {itemsControl.Bounds}, IsVisible={itemsControl.IsVisible}, IsEffectivelyVisible={itemsControl.IsEffectivelyVisible}");

            DumpTree(view, 0);

            var textBlocks = view.GetVisualDescendants().OfType<TextBlock>().Select(x => x.Text).ToArray();
            output.WriteLine($"TextBlocks: {string.Join(" | ", textBlocks)}");

            var vm = Assert.IsType<global::OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels.FumenObjectPropertyBrowserViewModel>(browser);
            var propertyViews = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control is IView && control.DataContext is CommonUIViewModelBase)
                .ToArray();

            Assert.True(itemsControl.ItemCount > 0, "ItemsControl has no items after RefreshSelected.");
            Assert.NotEmpty(realizedContainers);
            Assert.Equal(vm.PropertyInfoWrappers.Count, propertyViews.Length);
            Assert.All(propertyViews, propertyView =>
                Assert.True(propertyView.Bounds.Height > 0, $"{propertyView.GetType().Name} has no rendered height."));
            Assert.All(vm.PropertyInfoWrappers, wrapper =>
                Assert.Contains(wrapper.DisplayPropertyName, textBlocks));
        }
        finally
        {
            window.Close();
        }
    }
}
