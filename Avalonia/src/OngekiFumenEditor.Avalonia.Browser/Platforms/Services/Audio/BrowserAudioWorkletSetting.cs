using System;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Models.Settings;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

/// <summary>
/// Persisted tuning values for the browser AudioWorklet transport.
/// </summary>
public partial class BrowserAudioWorkletSetting : SettingModelBase<BrowserAudioWorkletSetting>
{
    public const int DefaultBufferDurationMilliseconds = 60;
    public const int MinBufferDurationMilliseconds = 20;
    public const int MaxBufferDurationMilliseconds = 5000;

    public const int DefaultInitialBufferFrameCount = 128;
    public const int MinInitialBufferFrameCount = 128;
    public const int MaxInitialBufferFrameCount = 8192;

    public const bool DefaultUseDeviceSampleRate = true;

    public static JsonTypeInfo<BrowserAudioWorkletSetting> JsonTypeInfo =>
        BrowserAudioWorkletSettingJsonContext.Default.BrowserAudioWorkletSetting;

    private static readonly Lazy<BrowserAudioWorkletSetting> defaultInstance =
        new(() => LoadDefault(JsonTypeInfo));

    public static BrowserAudioWorkletSetting Default => defaultInstance.Value;

    protected override JsonTypeInfo<BrowserAudioWorkletSetting> JsonTypeInfoCore => JsonTypeInfo;

    [ObservableProperty]
    public partial int BufferDurationMilliseconds { get; set; } = DefaultBufferDurationMilliseconds;

    [ObservableProperty]
    public partial int InitialBufferFrameCount { get; set; } = DefaultInitialBufferFrameCount;

    [ObservableProperty]
    public partial bool UseDeviceSampleRate { get; set; } = DefaultUseDeviceSampleRate;
}

[JsonSerializable(typeof(BrowserAudioWorkletSetting))]
public partial class BrowserAudioWorkletSettingJsonContext : JsonSerializerContext
{
}
