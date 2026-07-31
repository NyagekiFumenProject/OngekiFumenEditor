using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public partial class ProgramSetting : SettingModelBase<ProgramSetting>
{
    public static JsonTypeInfo<ProgramSetting> JsonTypeInfo => JsonSourceGenerateContext.Default.ProgramSetting;

    private static readonly Lazy<ProgramSetting> defaultInstance = new(() => LoadDefault(JsonTypeInfo));
    public static ProgramSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<ProgramSetting> JsonTypeInfoCore => JsonTypeInfo;

    [ObservableProperty]
    public partial string DumpFileDirPath { get; set; } = ".\\Dumps";

    [ObservableProperty]
    public partial bool IsFullDump { get; set; } = false;

    [ObservableProperty]
    public partial bool IsNotifyUserCrash { get; set; } = true;

    [ObservableProperty]
    public partial bool UpgradeProcessPriority { get; set; } = false;

    [ObservableProperty]
    public partial bool DisableShowSplashScreenAfterBoot { get; set; } = false;

    [ObservableProperty]
    public partial bool EnableMultiInstances { get; set; } = false;

    [ObservableProperty]
    public partial bool IsFirstTimeOpenEditor { get; set; } = true;

    [ObservableProperty]
    public partial string WindowSizePositionLastTime { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool UpdaterCheckMasterBranchOnly { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableUpdateCheck { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowConsoleWindowInGUIMode { get; set; } = false;

    [ObservableProperty]
    public partial bool DisableStringRendererAntialiasing { get; set; } = false;

    [ObservableProperty]
    public partial bool __NeedUpgradeSetting { get; set; } = true;
}
