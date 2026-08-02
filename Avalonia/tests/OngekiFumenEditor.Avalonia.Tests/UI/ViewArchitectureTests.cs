using Avalonia.Controls;
using Gekimini.Avalonia.ViewModels;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class ViewArchitectureTests
{
    private static readonly Type[] ViewTypes = typeof(FumenVisualEditorView).Assembly
        .GetTypes()
        .Where(type => type.IsPublic &&
                       !type.IsAbstract &&
                       type.Name.EndsWith("View", StringComparison.Ordinal) &&
                       typeof(Control).IsAssignableFrom(type))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    [Fact]
    public void AllViewControls_ImplementIView()
    {
        var invalidTypes = ViewTypes
            .Where(type => !typeof(IView).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(invalidTypes.Length == 0,
            $"The following view controls do not implement IView:{Environment.NewLine}{string.Join(Environment.NewLine, invalidTypes)}");
    }

    [Fact]
    public void AllUserControlViews_DeriveFromViewBase()
    {
        var invalidTypes = ViewTypes
            .Where(type => typeof(UserControl).IsAssignableFrom(type) &&
                           !typeof(ViewBase).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(invalidTypes.Length == 0,
            $"The following UserControl views do not derive from ViewBase:{Environment.NewLine}{string.Join(Environment.NewLine, invalidTypes)}");
    }

    [Fact]
    public void NamedViewModels_DeriveFromViewModelBase()
    {
        var assembly = typeof(FumenVisualEditorView).Assembly;
        var invalidPairs = ViewTypes
            .Select(viewType => (ViewType: viewType, ViewModelType: FindViewModelType(assembly, viewType)))
            .Where(pair => pair.ViewModelType is not null &&
                           !typeof(ViewModelBase).IsAssignableFrom(pair.ViewModelType))
            .Select(pair => $"{pair.ViewType.FullName} -> {pair.ViewModelType!.FullName}")
            .ToArray();

        Assert.True(invalidPairs.Length == 0,
            $"The following named view models do not derive from ViewModelBase:{Environment.NewLine}{string.Join(Environment.NewLine, invalidPairs)}");
    }

    private static Type? FindViewModelType(System.Reflection.Assembly assembly, Type viewType)
    {
        var viewNamespace = viewType.Namespace!;
        var viewModelName = $"{viewType.Name}Model";
        var candidateNamespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            $"{viewNamespace}.ViewModels"
        };

        if (viewNamespace.Contains(".Views.", StringComparison.Ordinal))
            candidateNamespaces.Add(viewNamespace.Replace(".Views.", ".ViewModels.", StringComparison.Ordinal));
        else if (viewNamespace.EndsWith(".Views", StringComparison.Ordinal))
            candidateNamespaces.Add($"{viewNamespace[..^".Views".Length]}.ViewModels");

        return candidateNamespaces
            .Select(candidateNamespace => assembly.GetType($"{candidateNamespace}.{viewModelName}"))
            .FirstOrDefault(type => type is not null);
    }
}
