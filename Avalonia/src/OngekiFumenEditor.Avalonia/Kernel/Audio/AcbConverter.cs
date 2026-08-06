using DereTore.Exchange.Archive.ACB;
using DereTore.Exchange.Audio.HCA;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;
using OngekiFumenEditor.Avalonia.Utils;
using System.Buffers;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public static class AcbConverter
{
    private static readonly SemaphoreSlim locker = new(1, 1);

    private static async Task ProcessAllBinaries(uint acbFormatVersion, string extractFilePath, Afs2Archive archive, Stream dataStream)
    {
        async Task DecodeHca(Stream hcaDataStream, Stream waveStream, DecodeParams decodeParams)
        {
            using var hcaStream = new OneWayHcaAudioStream(hcaDataStream, decodeParams, true);
            var buffer = ArrayPool<byte>.Shared.Rent(1_024_000);
            var read = 1;

            while (read > 0)
            {
                read = await hcaStream.ReadAsync(buffer, 0, buffer.Length);
                if (read > 0)
                    await waveStream.WriteAsync(buffer, 0, read);
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }

        foreach (var entry in archive.Files)
        {
            var record = entry.Value;
            var len = (int)record.FileLength;
            var buffer = ArrayPool<byte>.Shared.Rent(len);
            dataStream.Seek(record.FileOffsetAligned, SeekOrigin.Begin);
            var read = dataStream.Read(buffer, 0, len);
            using var fileData = new MemoryStream(buffer, 0, read);

            if (HcaReader.IsHcaStream(fileData))
            {
                Log.LogDebug($"Processing {acbFormatVersion} AFS: #{record.CueId} (offset={record.FileOffsetAligned} size={record.FileLength})...");
                try
                {
                    using var fs = File.Open(extractFilePath, FileMode.Create, FileAccess.Write, FileShare.Write);
                    await DecodeHca(fileData, fs, DecodeParams.Default);
                    Log.LogDebug("decoded");
                }
                catch (Exception ex)
                {
                    if (File.Exists(extractFilePath))
                        File.Delete(extractFilePath);
                    Log.LogDebug(ex.ToString());
                    if (ex.InnerException is not null)
                    {
                        Log.LogDebug("Details:");
                        Log.LogDebug(ex.InnerException.ToString());
                    }
                }
            }
            else
            {
                Log.LogDebug("skipped (not HCA)");
            }

            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task<ITemporaryFile> ConvertAcbFileToWavFile(
        string filePath,
        string validatedExternalAwbPath = null)
    {
        await locker.WaitAsync();
        try
        {
            var provider = IoC.Get<ITemporaryFolderProvider>();
            var tempFolder = await provider.Root.GetOrCreateFolderAsync("decodeAcbFiles");
            var temporaryFile = await provider.CreateUniqueFileAsync(
                "decoded",
                ".wav",
                tempFolder);
            var tempAwbFilePath = temporaryFile.GetRequiredLocalPath();
            Log.LogInfo("Decode ACB audio into a temporary WAV file.");

            try
            {
                using var acb = AcbFile.FromFile(filePath);
                var awb = acb.InternalAwb ?? acb.ExternalAwb
                    ?? throw new InvalidDataException("The ACB file has no AWB data.");
                using var awbStream = awb == acb.InternalAwb
                    ? acb.Stream
                    : File.OpenRead(validatedExternalAwbPath
                        ?? throw new InvalidDataException("External AWB access was not validated by the project session."));
                await ProcessAllBinaries(acb.FormatVersion, tempAwbFilePath, awb, awbStream);
                if (await temporaryFile.GetLengthAsync() <= 0)
                    throw new InvalidDataException("The ACB file did not produce decodable audio.");
                return temporaryFile;
            }
            catch (Exception e)
            {
                await temporaryFile.DeleteAsync();
                Log.LogError($"Load acb file failed : {e.Message}");
                return null;
            }
        }
        catch (Exception e)
        {
            Log.LogError($"Temporary ACB decode storage is unavailable : {e.Message}");
            return null;
        }
        finally
        {
            locker.Release();
        }
    }
}

