using Gekimini.Avalonia.Framework;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests;

public sealed class ProviderRegistrationTests
{
    [Fact]
    public async Task DesktopComposition_RegistersOneFumenProviderUnderBothInterfaces()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorDesktopCommandLine();
        await using var provider = services.BuildServiceProvider();

        var editorProvider = Assert.Single(
            provider.GetServices<IEditorProvider>().OfType<FumenVisualEditorProviderBase>());
        var fumenProvider = provider.GetRequiredService<IFumenVisualEditorProvider>();

        Assert.IsType<DefaultDesktopFumenVisualEditorProvider>(editorProvider);
        Assert.Same(editorProvider, fumenProvider);
        Assert.True(editorProvider.CanCreateNew);
        Assert.Single(editorProvider.FileTypes);
    }
}
