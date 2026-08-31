using System;
using NAudio.Wave.Browser;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

/// <summary>
/// Pure validation and transport-option mapping for browser audio settings.
/// </summary>
public static class BrowserAudioWorkletSettingRules
{
    public static BrowserAudioWorkletSetting Normalize(
        BrowserAudioWorkletSetting setting,
        out bool changed)
    {
        ArgumentNullException.ThrowIfNull(setting);

        changed = false;
        if (setting.BufferDurationMilliseconds is < BrowserAudioWorkletSetting.MinBufferDurationMilliseconds
            or > BrowserAudioWorkletSetting.MaxBufferDurationMilliseconds)
        {
            setting.BufferDurationMilliseconds = BrowserAudioWorkletSetting.DefaultBufferDurationMilliseconds;
            changed = true;
        }

        if (setting.InitialBufferFrameCount is < BrowserAudioWorkletSetting.MinInitialBufferFrameCount
            or > BrowserAudioWorkletSetting.MaxInitialBufferFrameCount)
        {
            setting.InitialBufferFrameCount = BrowserAudioWorkletSetting.DefaultInitialBufferFrameCount;
            changed = true;
        }

        return setting;
    }

    public static BrowserAudioWorkletOptions ToOptions(BrowserAudioWorkletSetting setting)
    {
        Normalize(setting, out _);
        return new BrowserAudioWorkletOptions
        {
            BufferDurationMilliseconds = setting.BufferDurationMilliseconds,
            InitialBufferFrameCount = setting.InitialBufferFrameCount,
            UseDeviceSampleRate = setting.UseDeviceSampleRate,
        };
    }
}
