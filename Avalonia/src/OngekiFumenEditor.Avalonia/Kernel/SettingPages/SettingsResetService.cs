using Gekimini.Avalonia.Modules.Settings;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages;

/// <summary>
/// Coordinates restoring every registered settings editor to its defaults.
/// The provider is intentionally retained so Browser and other platform
/// extensions can contribute editors after the core services are registered.
/// </summary>
public interface ISettingsResetService
{
    void ResetAll();
}

[RegisterSingleton<ISettingsResetService>]
public sealed class SettingsResetService : ISettingsResetService
{
    private readonly IServiceProvider serviceProvider;

    public SettingsResetService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public void ResetAll()
    {
        foreach (var editor in serviceProvider.GetServices<ISettingsEditor>())
            editor.ResetDefault();
    }
}
