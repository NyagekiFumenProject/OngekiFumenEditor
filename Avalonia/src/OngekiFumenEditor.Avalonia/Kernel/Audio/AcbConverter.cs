using DereTore.Exchange.Archive.ACB;
using DereTore.Exchange.Audio.HCA;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using System.Buffers;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public static class AcbConverter
{
    private static readonly SemaphoreSlim locker = new(1, 1);

    public static async Task ConvertAcbFileToWavAsync(
        Stream acbInputStream,
        Stream externalAwbInputStream,
        ISimpleFile outputWavFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acbInputStream);
        ArgumentNullException.ThrowIfNull(outputWavFile);

        await locker.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Log.LogInfo("Decode ACB audio into a WAV file.");

            var acbStream = await EnsureSeekableReadAsync(
                acbInputStream,
                cancellationToken).ConfigureAwait(false);
            try
            {
                if (AudioStreamFormatDetector.Detect(acbStream) != AudioStreamFormat.Acb)
                    throw new InvalidDataException("The first stream is not an ACB file.");

                if (externalAwbInputStream is not null)
                {
                    var externalAwbStream = await EnsureSeekableReadAsync(
                        externalAwbInputStream,
                        cancellationToken).ConfigureAwait(false);
                    try
                    {
                        using var externalAwb = new Afs2Archive(
                            externalAwbStream,
                            0,
                            "audio.awb",
                            disposeStream: false);
                        externalAwb.Initialize();

                        var converted = await WriteFirstHcaAsWavAsync(
                            0,
                            externalAwb,
                            externalAwbStream,
                            outputWavFile,
                            cancellationToken).ConfigureAwait(false);
                        if (!converted)
                            throw new InvalidDataException(
                                "The external AWB did not produce decodable audio.");
                        return;
                    }
                    finally
                    {
                        if (!ReferenceEquals(externalAwbStream, externalAwbInputStream))
                            await externalAwbStream.DisposeAsync().ConfigureAwait(false);
                    }
                }

                AcbFile acb;
                try
                {
                    acb = AcbFile.FromStream(
                        acbStream,
                        Path.Combine(AppContext.BaseDirectory, "stream-audio.acb"),
                        disposeStream: false);
                }
                catch (Exception exception) when (
                    exception is ArgumentException or FileNotFoundException)
                {
                    throw new InvalidDataException(
                        "The ACB file requires an external AWB stream.",
                        exception);
                }

                using (acb)
                {
                    if (acb.InternalAwb is { } internalAwb)
                    {
                        var converted = await WriteFirstHcaAsWavAsync(
                            acb.FormatVersion,
                            internalAwb,
                            acb.Stream,
                            outputWavFile,
                            cancellationToken).ConfigureAwait(false);
                        if (!converted)
                            throw new InvalidDataException(
                                "The embedded AWB did not produce decodable audio.");
                        return;
                    }

                    throw new InvalidDataException(
                        "The ACB file does not contain an embedded AWB stream. " +
                        "Pass its external AWB as the second stream argument.");
                }
            }
            finally
            {
                if (!ReferenceEquals(acbStream, acbInputStream))
                    await acbStream.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            locker.Release();
        }
    }

    private static async Task<bool> WriteFirstHcaAsWavAsync(
        uint acbFormatVersion,
        Afs2Archive archive,
        Stream dataStream,
        ISimpleFile outputWavFile,
        CancellationToken cancellationToken)
    {
        foreach (var record in archive.Files.Values.OrderBy(x => x.CueId))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (record.FileLength <= 0 || record.FileLength > int.MaxValue)
                continue;

            var buffer = ArrayPool<byte>.Shared.Rent(checked((int)record.FileLength));
            try
            {
                dataStream.Position = record.FileOffsetAligned;
                await dataStream.ReadExactlyAsync(
                    buffer.AsMemory(0, checked((int)record.FileLength)),
                    cancellationToken).ConfigureAwait(false);

                using var fileData = new MemoryStream(
                    buffer,
                    0,
                    checked((int)record.FileLength),
                    writable: false,
                    publiclyVisible: true);
                fileData.Position = 0;
                if (!HcaReader.IsHcaStream(fileData))
                    continue;

                fileData.Position = 0;
                Log.LogDebug(
                    $"Processing {acbFormatVersion} AFS: #{record.CueId} " +
                    $"(offset={record.FileOffsetAligned} size={record.FileLength})...");

                await outputWavFile.WriteAsync(
                    (outputStream, writerCancellationToken) =>
                        DecodeHcaAsync(fileData, outputStream, writerCancellationToken),
                    cancellationToken).ConfigureAwait(false);
                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        return false;
    }

    private static async Task DecodeHcaAsync(
        Stream hcaDataStream,
        Stream waveStream,
        CancellationToken cancellationToken)
    {
        using var hcaStream = new OneWayHcaAudioStream(
            hcaDataStream,
            DecodeParams.Default,
            outputWaveHeader: true);
        var buffer = ArrayPool<byte>.Shared.Rent(1_024_000);
        try
        {
            while (true)
            {
                var read = await hcaStream.ReadAsync(
                    buffer.AsMemory(),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                await waveStream.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<Stream> EnsureSeekableReadAsync(
        Stream sourceStream,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceStream.CanSeek)
        {
            sourceStream.Position = 0;
            return sourceStream;
        }

        var buffer = new MemoryStream();
        await sourceStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return buffer;
    }
}
