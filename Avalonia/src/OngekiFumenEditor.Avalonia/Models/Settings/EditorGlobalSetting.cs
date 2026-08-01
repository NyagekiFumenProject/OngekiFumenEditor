using System.Drawing;
using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Models.Settings;

public partial class EditorGlobalSetting : SettingModelBase<EditorGlobalSetting>
{
    public static JsonTypeInfo<EditorGlobalSetting> JsonTypeInfo => OngekiJsonSourceGenerateContext.Default.EditorGlobalSetting;

    private static readonly Lazy<EditorGlobalSetting> defaultInstance = new(() => LoadDefault(JsonTypeInfo));
    public static EditorGlobalSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<EditorGlobalSetting> JsonTypeInfoCore => JsonTypeInfo;

    [ObservableProperty]
    public partial int AutoSaveTimeInterval { get; set; } = 5;

    [ObservableProperty]
    public partial bool IsEnableAutoSave { get; set; } = true;

    [ObservableProperty]
    public partial int UndoActionSavingLimit { get; set; } = 50;

    [ObservableProperty]
    public partial bool IsEnableUndoActionSavingLimit { get; set; } = false;

    [ObservableProperty]
    public partial double JudgeLineOffsetY { get; set; } = 50;

    [ObservableProperty]
    public partial bool DisableXGridMagneticDock { get; set; } = false;

    [ObservableProperty]
    public partial bool ForceMagneticDock { get; set; } = false;

    [ObservableProperty]
    public partial bool ForceTapHoldMagneticDockToLane { get; set; } = false;

    [ObservableProperty]
    public partial bool DisableTGridMagneticDock { get; set; } = false;

    [ObservableProperty]
    public partial double XGridUnitSpace { get; set; } = 4;

    [ObservableProperty]
    public partial int TGridUnitLength { get; set; } = 240;

    [ObservableProperty]
    public partial int BeatSplit { get; set; } = 1;

    [ObservableProperty]
    public partial int XGridDisplayMaxUnit { get; set; } = 64;

    [ObservableProperty]
    public partial bool ForceXGridMagneticDock { get; set; } = false;

    [ObservableProperty]
    public partial double VerticalDisplayScale { get; set; } = 0.75;

    [ObservableProperty]
    public partial int DisplayTimeFormat { get; set; } = 0;

    [ObservableProperty]
    public partial bool JudgeLineAlignBeat { get; set; } = false;

    [ObservableProperty]
    public partial int MouseWheelLength { get; set; } = 50;

    [ObservableProperty]
    public partial double XOffset { get; set; } = 0;

    [ObservableProperty]
    public partial bool ShowXOffsetScrollBar { get; set; } = true;

    [ObservableProperty]
    public partial bool EnableXOffset { get; set; } = true;

    [ObservableProperty]
    public partial bool AdjustPastedObjects { get; set; } = false;

    [ObservableProperty]
    public partial int ParallelCountLimit { get; set; } = 3000;

    [ObservableProperty]
    public partial string RecentOpenedListStr { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RecoveryCurrentTimeAfterExecuteAction { get; set; } = false;

    [ObservableProperty]
    public partial bool LoopPlayTiming { get; set; } = false;

    [ObservableProperty]
    public partial bool EnablePlaceObjectBeyondAudioDuration { get; set; } = false;

    [ObservableProperty]
    public partial int PlayFieldForegroundColor { get; set; } = -16777216;

    [ObservableProperty]
    public partial int PlayFieldBackgroundColor { get; set; } = -16758145;

    [ObservableProperty]
    public partial bool EnablePlayFieldDrawing { get; set; } = false;

    [ObservableProperty]
    public partial bool EnableShowPlayerLocation { get; set; } = false;

    [ObservableProperty]
    public partial Color ColorHoldLeft { get; set; } = Color.Red;

    [ObservableProperty]
    public partial Color ColorHoldCenter { get; set; } = Color.Lime;

    [ObservableProperty]
    public partial Color ColorHoldRight { get; set; } = Color.Blue;

    [ObservableProperty]
    public partial Color ColorHoldWallRight { get; set; } = Color.FromArgb(35, 4, 117);

    [ObservableProperty]
    public partial Color ColorHoldWallLeft { get; set; } = Color.FromArgb(136, 3, 152);

    [ObservableProperty]
    public partial int LimitFPS { get; set; } = -1;

    [ObservableProperty]
    public partial string RenderTargetOrderVisibleMap { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HideWallLaneWhenEnablePlayField { get; set; } = false;

    [ObservableProperty]
    public partial bool ReturnStartTimeAfterPause { get; set; } = false;

    [ObservableProperty]
    public partial bool ShowHitObjectEffectInPreviewMode { get; set; } = true;
}
