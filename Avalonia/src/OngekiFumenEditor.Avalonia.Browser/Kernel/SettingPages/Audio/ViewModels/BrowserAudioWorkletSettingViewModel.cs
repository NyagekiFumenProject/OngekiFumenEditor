using System;
using Gekimini.Avalonia.Modules.Settings;
using Gekimini.Avalonia.ViewModels;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Browser.Assets.Languages;
using OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

namespace OngekiFumenEditor.Avalonia.Browser.Kernel.SettingPages.Audio.ViewModels;

[RegisterSingleton<ISettingsEditor>]
public partial class BrowserAudioWorkletSettingViewModel : ViewModelBase, ISettingsEditor
{
    private readonly BrowserAudioWorkletSetting setting;
    private decimal? bufferDurationMillisecondsValue;
    private decimal? initialBufferFrameCountValue;
    private bool useDeviceSampleRate;

    public BrowserAudioWorkletSettingViewModel()
    {
        setting = BrowserAudioWorkletSetting.Default;
        BrowserAudioWorkletSettingRules.Normalize(setting, out _);
        RefreshEditorValues();
    }

    public BrowserAudioWorkletSetting Setting => setting;

    public string SettingsPageName => BrowserAudioWorkletLang.NAudioBrowserAudioWorklet;

    public string SettingsPagePath => Lang.TabSound;

    /// <summary>
    /// Decimal adapter required by Avalonia NumericUpDown.Value.
    /// </summary>
    public decimal? BufferDurationMillisecondsValue
    {
        get => bufferDurationMillisecondsValue;
        set
        {
            if (value is null)
            {
                RefreshBufferDurationValue();
                return;
            }

            var normalized = NormalizeBufferDuration(value.Value);
            if (!SetProperty(ref bufferDurationMillisecondsValue, normalized))
                return;

            setting.BufferDurationMilliseconds = (int)normalized;
        }
    }

    /// <summary>
    /// Decimal adapter required by Avalonia NumericUpDown.Value.
    /// </summary>
    public decimal? InitialBufferFrameCountValue
    {
        get => initialBufferFrameCountValue;
        set
        {
            if (value is null)
            {
                RefreshInitialBufferFrameCountValue();
                return;
            }

            var normalized = NormalizeInitialBufferFrameCount(value.Value);
            if (!SetProperty(ref initialBufferFrameCountValue, normalized))
                return;

            setting.InitialBufferFrameCount = (int)normalized;
        }
    }

    public bool UseDeviceSampleRate
    {
        get => useDeviceSampleRate;
        set
        {
            if (!SetProperty(ref useDeviceSampleRate, value))
                return;

            setting.UseDeviceSampleRate = value;
        }
    }

    public void ApplyChanges()
    {
        // Numeric adapters update the model as the user edits; normalize once
        // more before persisting in case a caller set the model directly.
        BrowserAudioWorkletSettingRules.Normalize(setting, out _);
        RefreshEditorValues();
        setting.Save();
    }

    public void ResetDefault()
    {
        setting.Reset();
        RefreshEditorValues();
        setting.Save();
    }

    private void RefreshEditorValues()
    {
        bufferDurationMillisecondsValue = setting.BufferDurationMilliseconds;
        initialBufferFrameCountValue = setting.InitialBufferFrameCount;
        useDeviceSampleRate = setting.UseDeviceSampleRate;
        OnPropertyChanged(nameof(BufferDurationMillisecondsValue));
        OnPropertyChanged(nameof(InitialBufferFrameCountValue));
        OnPropertyChanged(nameof(UseDeviceSampleRate));
    }

    private void RefreshBufferDurationValue()
    {
        var value = (decimal)setting.BufferDurationMilliseconds;
        if (bufferDurationMillisecondsValue != value)
        {
            bufferDurationMillisecondsValue = value;
            OnPropertyChanged(nameof(BufferDurationMillisecondsValue));
        }
    }

    private void RefreshInitialBufferFrameCountValue()
    {
        var value = (decimal)setting.InitialBufferFrameCount;
        if (initialBufferFrameCountValue != value)
        {
            initialBufferFrameCountValue = value;
            OnPropertyChanged(nameof(InitialBufferFrameCountValue));
        }
    }

    private static decimal NormalizeBufferDuration(decimal value) =>
        Math.Clamp(
            decimal.Round(value, 0, MidpointRounding.AwayFromZero),
            BrowserAudioWorkletSetting.MinBufferDurationMilliseconds,
            BrowserAudioWorkletSetting.MaxBufferDurationMilliseconds);

    private static decimal NormalizeInitialBufferFrameCount(decimal value) =>
        Math.Clamp(
            decimal.Round(value, 0, MidpointRounding.AwayFromZero),
            BrowserAudioWorkletSetting.MinInitialBufferFrameCount,
            BrowserAudioWorkletSetting.MaxInitialBufferFrameCount);
}
