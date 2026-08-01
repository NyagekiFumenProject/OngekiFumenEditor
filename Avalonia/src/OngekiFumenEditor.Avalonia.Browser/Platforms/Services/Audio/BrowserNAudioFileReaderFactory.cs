using Injectio.Attributes;
using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using System;
using System.Collections.Generic;
using System.IO;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

[RegisterSingleton<INAudioFileReaderFactory>]
internal sealed class BrowserNAudioFileReaderFactory : INAudioFileReaderFactory
{
    public IReadOnlyList<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; } =
    [
        (".wav", "Audio File"),
        (".aif", "Audio File"),
        (".aiff", "Audio File")
    ];

    public WaveStream CreateAudioFileReader(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".wav" => CreateWaveFileReader(filePath),
            ".aif" or ".aiff" => new AiffFileReader(filePath),
            _ => throw new PlatformNotSupportedException(
                "Browser audio decoding currently supports PCM/IEEE-float WAV and AIFF files.")
        };

    private static WaveStream CreateWaveFileReader(string filePath)
    {
        var reader = new WaveFileReader(filePath);
        var format = reader.WaveFormat.AsStandardWaveFormat();
        if (format.Encoding is WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat)
            return reader;

        reader.Dispose();
        throw new NotSupportedException(
            "The browser audio backend supports PCM and IEEE-float WAV files only.");
    }
}
