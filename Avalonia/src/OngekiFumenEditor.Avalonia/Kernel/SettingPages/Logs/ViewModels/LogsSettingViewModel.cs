using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.ViewModels;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Platforms.Services.Logging;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Logs.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class LogsSettingViewModel : ViewModelBase, ISettingsEditor
{
    public string LogFolderPath { get; }

    public string SettingsPageName => Lang.TabLogger;

    public string SettingsPagePath => Lang.TabEnviorment;

    public LogsSettingViewModel(ILogFileStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        LogFolderPath = storage.LogDirectoryPath;
    }

    public void ApplyChanges()
    {
        // The location is an effective platform capability, not an editable application setting.
    }
}
