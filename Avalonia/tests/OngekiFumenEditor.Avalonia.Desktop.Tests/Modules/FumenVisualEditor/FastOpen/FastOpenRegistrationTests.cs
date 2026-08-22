using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Gemini.Framework.Menus;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Desktop.CommandLine;
using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;
using OngekiFumenEditor.Avalonia.Desktop.Modules.SplashScreen.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.Modules.FumenVisualEditor.FastOpen;

public sealed class FastOpenRegistrationTests
{
    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorDesktopCommandLine();
        return services;
    }

    [Fact]
    public void DesktopComposition_RegistersFastOpenServiceHandlerAndDefinition()
    {
        var services = BuildServices();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(DesktopFastOpenService) &&
            descriptor.ImplementationType == typeof(DesktopFastOpenService));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommandHandler) &&
            descriptor.ImplementationType == typeof(FastOpenFumenCommandHandler));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(CommandDefinitionBase) &&
            descriptor.ImplementationType == typeof(FastOpenFumenCommandDefinition));
    }

    [Fact]
    public void DesktopSplashScreenViewModel_ExposesFastOpenCommand()
    {
        Assert.NotNull(typeof(DesktopSplashScreenViewModel).GetProperty("FastOpenCommand"));
    }

    [Fact]
    public void FastOpenShortcut_IsCtrlF()
    {
        var shortcut = Assert.IsType<CommandKeyboardShortcut<FastOpenFumenCommandDefinition>>(
            FastOpenFumenCommandDefinition.KeyGesture);

        Assert.Equal(new KeyGesture(Key.F, KeyModifiers.Control), shortcut.KeyGesture);
    }

    [Fact]
    public void FastOpenMenuItem_IsRegisteredOnFileNewOpenGroup()
    {
        Assert.IsType<CommandMenuItemDefinition<FastOpenFumenCommandDefinition>>(
            MenuDefinitions.FastOpenFumenMenuItem);
    }

    [Fact]
    public void CoreAssembly_ContainsNoFastOpenTypes()
    {
        var coreAssembly = typeof(FumenVisualEditorProviderBase).Assembly;

        Assert.DoesNotContain(
            coreAssembly.GetTypes(),
            type => type.Name.Contains("FastOpen", StringComparison.OrdinalIgnoreCase));
    }
}
