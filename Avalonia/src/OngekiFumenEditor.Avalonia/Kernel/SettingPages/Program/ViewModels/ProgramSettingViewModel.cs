using Gekimini.Avalonia.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Settings;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages;
using OngekiFumenEditor.Avalonia.Models.Settings;
using Microsoft.Extensions.DependencyInjection;
using SimpleTypedLocalizer;

namespace OngekiFumenEditor.Avalonia.Kernel.SettingPages.Program.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class ProgramSettingViewModel : ViewModelBase, ISettingsEditor
{
    private readonly ISettingsResetService settingsResetService;

    public ProgramSetting Setting => ProgramSetting.Default;

    public string SettingsPageName => Lang.TabProgram;

    public string SettingsPagePath => Lang.TabEnviorment;

    public ProgramSettingViewModel()
        : this(null)
    {
    }

    [ActivatorUtilitiesConstructor]
    public ProgramSettingViewModel(ISettingsResetService settingsResetService)
    {
        this.settingsResetService = settingsResetService;
        Setting.PropertyChanged += (_, e) => Log.LogDebug($"program setting property changed : {e.PropertyName}");
    }

    public void ApplyChanges()
    {
        Setting.Save();
    }

    public void ResetDefault()
    {
        Setting.Reset();
        Setting.Save();
    }

    [RelayCommand]
    private async Task ResetAllSettingsAsync()
    {
        Log.LogInfo("ResetAllSettingsAsync triggered.");
        if (!await IoC.Get<IDialogManager>().ShowComfirmDialog(Lang.ResetAllSettingComfirm, Lang.Warning))
            return;

        (settingsResetService ?? IoC.Get<ISettingsResetService>()).ResetAll();

        await IoC.Get<IDialogManager>().ShowMessageDialog(Lang.ResetCompleted);
    }

    [RelayCommand]
    private async Task SelectDumpFolderAsync()
    {
        Log.LogInfo("SelectDumpFolderAsync triggered.");
        using var folder = await FileDialogHelper.OpenDirectoryAsync(Lang.CrashDumpFileOutput);
        if (string.IsNullOrWhiteSpace(folder?.FullPath))
            return;

        Setting.DumpFileDirPath = folder.FullPath;
        ApplyChanges();
    }

    [RelayCommand]
    private void ThrowException()
    {
        Log.LogInfo("ThrowException triggered.");
        _ = Task.Run(() => throw new Exception("Crash dump test exception."));
    }

    public sealed record CrashDumpLevelOption(CrashDumpLevel Level, ILocalizedTextSource Name);

    public IReadOnlyList<CrashDumpLevelOption> CrashDumpLevels { get; } =
    [
        new(CrashDumpLevel.Small, Lang.B.CrashDumpSmall),
        new(CrashDumpLevel.Medium, Lang.B.CrashDumpMedium),
        new(CrashDumpLevel.Full, Lang.B.CrashDumpFull),
    ];

    public CrashDumpLevelOption SelectedCrashDumpLevel
    {
        get => CrashDumpLevels.FirstOrDefault(o => o.Level == Setting.DumpLevel) ?? CrashDumpLevels[0];
        set
        {
            if (value is null || value.Level == Setting.DumpLevel)
                return;

            Setting.DumpLevel = value.Level;
            OnPropertyChanged();
            ApplyChanges();
        }
    }

}
