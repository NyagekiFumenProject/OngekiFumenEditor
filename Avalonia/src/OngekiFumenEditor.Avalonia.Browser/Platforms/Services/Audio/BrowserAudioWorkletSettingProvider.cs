using System;
using System.Diagnostics;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Browser.Utils.Interops;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

public interface IBrowserAudioWorkletSettingProvider
{
    BrowserAudioWorkletSetting Load();
    void Save(BrowserAudioWorkletSetting setting);
}

[RegisterSingleton<IBrowserAudioWorkletSettingProvider>]
public sealed class BrowserAudioWorkletSettingProvider : IBrowserAudioWorkletSettingProvider
{
    private readonly BrowserAudioWorkletSettingStore store = new(
        () => LocalStorageInterop.Load(BrowserAudioWorkletSettingStore.StorageKey),
        value => LocalStorageInterop.Save(BrowserAudioWorkletSettingStore.StorageKey, value),
        ReportWriteFailure);

    public BrowserAudioWorkletSetting Load() => store.Load();

    public void Save(BrowserAudioWorkletSetting setting) => store.Save(setting);

    private static void ReportWriteFailure(Exception exception)
    {
        try
        {
            global::OngekiFumenEditor.Avalonia.Utils.Log.LogWarning(
                $"Failed to persist Browser AudioWorklet settings: {exception.Message}");
        }
        catch
        {
            Debug.WriteLine(exception);
        }
    }
}
