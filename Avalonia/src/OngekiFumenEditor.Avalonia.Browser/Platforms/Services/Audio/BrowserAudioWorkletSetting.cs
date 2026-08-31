using System.Text.Json.Serialization;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

/// <summary>
/// Persisted tuning values for the browser AudioWorklet transport.
/// </summary>
public sealed class BrowserAudioWorkletSetting
{
    public const int DefaultBufferDurationMilliseconds = 20;
    public const int MinBufferDurationMilliseconds = 20;
    public const int MaxBufferDurationMilliseconds = 5000;

    public const int DefaultInitialBufferFrameCount = 512;
    public const int MinInitialBufferFrameCount = 128;
    public const int MaxInitialBufferFrameCount = 8192;

    public const bool DefaultUseDeviceSampleRate = true;

    public int BufferDurationMilliseconds { get; set; } = DefaultBufferDurationMilliseconds;

    public int InitialBufferFrameCount { get; set; } = DefaultInitialBufferFrameCount;

    public bool UseDeviceSampleRate { get; set; } = DefaultUseDeviceSampleRate;
}

[JsonSerializable(typeof(BrowserAudioWorkletSetting))]
public partial class BrowserAudioWorkletSettingJsonContext : JsonSerializerContext
{
}
