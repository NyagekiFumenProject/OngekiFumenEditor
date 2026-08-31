using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.Modules.MainMenu.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.DependencyInjection;

public sealed class SettingsResetServiceTests
{
    [Fact]
    public void CoreRegistrationProvidesSingletonResetService()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvalonia();

        var descriptor = Assert.Single(services,
            service => service.ServiceType == typeof(ISettingsResetService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(SettingsResetService), descriptor.ImplementationType);
    }

    [Fact]
    public void MainMenuEditorIsRegisteredAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddGekiminiAvalonia();

        var descriptor = Assert.Single(services,
            service => service.ServiceType == typeof(ISettingsEditor) &&
                       service.ImplementationType == typeof(MainMenuSettingsViewModel));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void ResetAllReenumeratesEditorsInRegistrationOrderWithoutDeduplication()
    {
        var calls = new List<string>();
        var first = new RecordingEditor("first", calls);
        var second = new RecordingEditor("second", calls);
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsEditor>(first);
        services.AddSingleton<ISettingsEditor>(second);
        services.AddSingleton<ISettingsEditor>(first);

        using var provider = services.BuildServiceProvider();
        var resetService = new SettingsResetService(provider);

        resetService.ResetAll();
        resetService.ResetAll();

        Assert.Equal(
            ["first", "second", "first", "first", "second", "first"],
            calls);
        Assert.Equal(4, first.ResetCount);
        Assert.Equal(2, second.ResetCount);
    }

    private sealed class RecordingEditor(string name, IList<string> calls) : ISettingsEditor
    {
        public int ResetCount { get; private set; }

        public string SettingsPageName => name;

        public string SettingsPagePath => string.Empty;

        public void ApplyChanges()
        {
        }

        public void ResetDefault()
        {
            ResetCount++;
            calls.Add(name);
        }
    }
}
