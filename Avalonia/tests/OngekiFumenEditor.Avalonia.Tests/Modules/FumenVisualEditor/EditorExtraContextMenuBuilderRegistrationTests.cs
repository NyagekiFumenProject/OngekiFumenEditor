using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel.DefaultImpl;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class EditorExtraContextMenuBuilderRegistrationTests
{
    [Fact]
    public void AddOngekiFumenEditorAvalonia_RegistersSingletonEditorExtraContextMenuBuilder()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvalonia();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IEditorExtraContextMenuBuilder>();
        var second = provider.GetRequiredService<IEditorExtraContextMenuBuilder>();

        Assert.IsType<DefaultEditorExtraContextMenuBuilder>(first);
        Assert.Same(first, second);
    }
}
