using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.ViewModels;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Utils.Logs;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Logs.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class LogsSettingViewModel : ViewModelBase, ISettingsEditor
{
    public string LogFolderPath { get; }

    public string SettingsPageName => Lang.TabLogger;

    public string SettingsPagePath => Lang.TabEnviorment;

    public LogsSettingViewModel(IEnumerable<ILogOutput> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        LogFolderPath = outputs.OfType<IFileLogOutput>().FirstOrDefault()?.LogDirectoryPath ?? string.Empty;
    }

    public void ApplyChanges()
    {
        // The location is an effective platform capability, not an editable application setting.
    }

    public void ResetDefault()
    {
        LogSetting.Default.Reset();
        LogSetting.Default.Save();
    }
}
