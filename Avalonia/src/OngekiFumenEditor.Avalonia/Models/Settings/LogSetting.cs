using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public partial class LogSetting : SettingModelBase<LogSetting>
{
    public static JsonTypeInfo<LogSetting> JsonTypeInfo => JsonSourceGenerateContext.Default.LogSetting;

    private static readonly Lazy<LogSetting> defaultInstance = new(() => LoadDefault(JsonTypeInfo));
    public static LogSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<LogSetting> JsonTypeInfoCore => JsonTypeInfo;

    [ObservableProperty]
    public partial string LogFileDirPath { get; set; } = ".\\Logs";
}
