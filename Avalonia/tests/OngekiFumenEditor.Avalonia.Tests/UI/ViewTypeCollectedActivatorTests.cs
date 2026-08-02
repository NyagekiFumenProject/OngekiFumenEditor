using System.Collections;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;
using Xunit;
using AppViewTypeCollectedActivator = OngekiFumenEditor.Avalonia.Avalonia.ViewTypeCollectedActivator;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class ViewTypeCollectedActivatorTests
{
    private static readonly Type[] ViewLocatorViewTypes = typeof(FumenVisualEditorView).Assembly
        .GetTypes()
        .Where(type => type.IsPublic &&
                       !type.IsAbstract &&
                       type.Name.EndsWith("View", StringComparison.Ordinal) &&
                       typeof(Control).IsAssignableFrom(type))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void Default_ContainsFactoriesForAllViewLocatorViews()
    {
        var activator = AppViewTypeCollectedActivator.Default;

        Assert.NotNull(activator);

        var field = typeof(AppViewTypeCollectedActivator).GetField("_typeFactories", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var factories = Assert.IsAssignableFrom<IDictionary>(field.GetValue(null));
        var registeredTypeNames = factories.Keys.Cast<string>().ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(ViewLocatorViewTypes);
        Assert.All(ViewLocatorViewTypes, type =>
            Assert.Contains(type.FullName!, registeredTypeNames));
    }

    [AvaloniaFact]
    public void TryCreateInstance_CreatesFumenVisualEditorView()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITypeCollectedActivator<IView>>(AppViewTypeCollectedActivator.Default);
        using var serviceProvider = services.BuildServiceProvider();

        var created = TypeCollectedActivatorHelper<IView>.TryCreateInstance(
            serviceProvider,
            typeof(FumenVisualEditorView).FullName!,
            out var view);

        Assert.True(created);
        Assert.IsType<FumenVisualEditorView>(view);
    }
}
