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
        (".aiff", "Audio File"),
        // ACB never reaches the reader factory directly; NAudioManager converts an ACB into a
        // temporary WAV first. The extension only enables the ACB branch and project selection.
        (".acb", "ACB Audio Package")
    ];

    public WaveStream CreateAudioFileReader(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".wav" => CreateWaveFileReader(filePath),
            ".aif" or ".aiff" => new AiffFileReader(filePath),
            _ => throw new PlatformNotSupportedException(
                "Browser audio decoding currently supports PCM/IEEE-float WAV and AIFF files.")
        };

    public WaveStream CreateAudioFileReader(Stream stream, AudioStreamFormat format)
        => format switch
        {
            AudioStreamFormat.Wav => CreateWaveFileReader(stream),
            AudioStreamFormat.Aiff => new AiffFileReader(stream),
            _ => throw new PlatformNotSupportedException(
                "Browser audio decoding currently supports PCM/IEEE-float WAV and AIFF files.")
        };

    private static WaveStream CreateWaveFileReader(string filePath)
    {
        return EnsureSupportedWaveFormat(new WaveFileReader(filePath));
    }

    private static WaveStream CreateWaveFileReader(Stream stream)
    {
        return EnsureSupportedWaveFormat(new WaveFileReader(stream));
    }

    private static WaveStream EnsureSupportedWaveFormat(WaveStream reader)
    {
        var format = reader.WaveFormat.AsStandardWaveFormat();
        if (format.Encoding is WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat)
            return reader;

        reader.Dispose();
        throw new NotSupportedException(
            "The browser audio backend supports PCM and IEEE-float WAV files only.");
    }
}
