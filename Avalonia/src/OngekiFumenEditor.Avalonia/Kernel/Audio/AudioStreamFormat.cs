namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public enum AudioStreamFormat
{
    Unknown,
    Wav,
    Aiff,
    Mp3,
    Acb,
    Awb
}

public static class AudioStreamFormatDetector
{
    public static AudioStreamFormat Detect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
            throw new NotSupportedException("Audio format detection requires a seekable stream.");

        var originalPosition = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[12];
            var read = 0;
            while (read < header.Length)
            {
                var count = stream.Read(header[read..]);
                if (count == 0)
                    break;
                read += count;
            }

            if (read >= 12 &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header[8..12].SequenceEqual("WAVE"u8))
            {
                return AudioStreamFormat.Wav;
            }

            if (read >= 12 &&
                header[..4].SequenceEqual("FORM"u8) &&
                (header[8..12].SequenceEqual("AIFF"u8) ||
                 header[8..12].SequenceEqual("AIFC"u8)))
            {
                return AudioStreamFormat.Aiff;
            }

            if (read >= 4 && header[..4].SequenceEqual("@UTF"u8))
                return AudioStreamFormat.Acb;

            if (read >= 4 && header[..4].SequenceEqual("AFS2"u8))
                return AudioStreamFormat.Awb;

            if (read >= 3 && header[..3].SequenceEqual("ID3"u8))
                return AudioStreamFormat.Mp3;

            if (read >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0)
                return AudioStreamFormat.Mp3;

            return AudioStreamFormat.Unknown;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    public static async Task<Stream> EnsureSeekableAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        cancellationToken.ThrowIfCancellationRequested();
        if (stream.CanSeek)
        {
            stream.Position = 0;
            return stream;
        }

        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return buffer;
    }
}
