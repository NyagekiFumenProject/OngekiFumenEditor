using System;
using System.Diagnostics;
using System.Text.Json;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

/// <summary>
/// Loads and persists browser AudioWorklet settings while keeping storage
/// access injectable for tests and non-browser hosts.
/// </summary>
public sealed class BrowserAudioWorkletSettingStore
{
    public const string StorageKey =
        "__browserPersistence_OngekiFumenEditor_Avalonia_BrowserAudioWorkletSetting";

    private readonly Func<string> read;
    private readonly Action<string> write;
    private readonly Action<Exception> reportWriteFailure;

    public BrowserAudioWorkletSettingStore(
        Func<string> read,
        Action<string> write,
        Action<Exception> reportWriteFailure = null)
    {
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        this.write = write ?? throw new ArgumentNullException(nameof(write));
        this.reportWriteFailure = reportWriteFailure ?? (exception => Debug.WriteLine(exception));
    }

    public BrowserAudioWorkletSetting Load()
    {
        string raw;
        try
        {
            raw = read();
        }
        catch (Exception exception)
        {
            // A storage read failure should not prevent audio from starting.
            Debug.WriteLine(exception);
            return new BrowserAudioWorkletSetting();
        }

        if (string.IsNullOrWhiteSpace(raw))
            return new BrowserAudioWorkletSetting();

        BrowserAudioWorkletSetting setting;
        var corrected = false;
        try
        {
            setting = JsonSerializer.Deserialize(
                          raw,
                          BrowserAudioWorkletSettingJsonContext.Default.BrowserAudioWorkletSetting)
                      ?? throw new JsonException("The browser audio setting document was null.");
            BrowserAudioWorkletSettingRules.Normalize(setting, out corrected);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            setting = new BrowserAudioWorkletSetting();
            corrected = true;
        }

        if (corrected)
            TryWrite(setting);

        return setting;
    }

    public void Save(BrowserAudioWorkletSetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);
        BrowserAudioWorkletSettingRules.Normalize(setting, out _);
        TryWrite(setting);
    }

    private void TryWrite(BrowserAudioWorkletSetting setting)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                setting,
                BrowserAudioWorkletSettingJsonContext.Default.BrowserAudioWorkletSetting);
            write(json);
        }
        catch (Exception exception)
        {
            // Persisting is best effort; callers continue with the normalized
            // in-memory values when localStorage is unavailable or full.
            try
            {
                reportWriteFailure(exception);
            }
            catch
            {
                Debug.WriteLine(exception);
            }
        }
    }
}
