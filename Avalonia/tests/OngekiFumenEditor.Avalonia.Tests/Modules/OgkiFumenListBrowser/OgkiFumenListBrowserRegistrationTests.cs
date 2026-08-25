using Gemini.Framework.Menus;
using Gekimini.Avalonia.Framework.Commands;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Commands;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.ViewModels;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.OgkiFumenListBrowser;

public sealed class OgkiFumenListBrowserRegistrationTests
{
    [Fact]
    public void AddOngekiFumenEditorAvalonia_RegistersWindowCommandMenuAndInterface()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvalonia();

        var windowDescriptor = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOgkiFumenListBrowser));
        Assert.Equal(ServiceLifetime.Singleton, windowDescriptor.Lifetime);
        Assert.Equal(typeof(OgkiFumenListBrowserViewModel), windowDescriptor.ImplementationType);

        var commandDescriptor = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(CommandDefinitionBase) &&
            descriptor.ImplementationType == typeof(ViewOgkiFumenListBrowserCommandDefinition));
        Assert.Equal(ServiceLifetime.Singleton, commandDescriptor.Lifetime);

        var handlerDescriptor = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(ICommandHandler) &&
            descriptor.ImplementationType == typeof(ViewOgkiFumenListBrowserCommandHandler));
        Assert.Equal(ServiceLifetime.Singleton, handlerDescriptor.Lifetime);

        var menuDescriptor = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(MenuItemDefinition) &&
            descriptor.ImplementationInstance is MenuItemDefinition item &&
            ReferenceEquals(item, MenuDefinitions.ViewOgkiFumenListBrowserMenuItem));
        Assert.Equal(ServiceLifetime.Singleton, menuDescriptor.Lifetime);
    }
}
