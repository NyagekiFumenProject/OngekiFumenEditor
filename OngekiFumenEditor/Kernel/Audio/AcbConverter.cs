using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using DereTore.Exchange.Archive.ACB;
using DereTore.Exchange.Audio.HCA;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Kernel.Audio;

public static class AcbConverter
{
    private static readonly object locker = new();

    private static async Task ProcessAllBinaries(uint acbFormatVersion, string extractFilePath, Afs2Archive archive,
        Stream dataStream)
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
            var len = (int) record.FileLength;
            var buffer = ArrayPool<byte>.Shared.Rent(len);
            dataStream.Seek(record.FileOffsetAligned, SeekOrigin.Begin);
            var read = dataStream.Read(buffer, 0, len);
            var fileData = new MemoryStream(buffer, 0, read);

            if (HcaReader.IsHcaStream(fileData))
            {
                Log.LogDebug(string.Format("Processing {0} AFS: #{1} (offset={2} size={3})...   ", acbFormatVersion,
                    record.CueId, record.FileOffsetAligned, record.FileLength));

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

                    if (ex.InnerException != null)
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

    public static async Task<string> ConvertAcbFileToWavFile(string filePath)
    {
        string tempAwbFilePath;
        lock (locker)
        {
            var tempFolder = TempFileHelper.GetTempFolderPath(prefix: "decodeAcbFiles", random: false);
            tempAwbFilePath = Path.Combine(tempFolder, Path.GetFileNameWithoutExtension(filePath) + ".wav");
            Log.LogInfo($"Extract .acb to .wav and load the later , acb file path : {tempAwbFilePath}");
            
            if (File.Exists(tempAwbFilePath))
            {
                Log.LogInfo($"use cache file: {tempAwbFilePath}");
                return tempAwbFilePath;
            }
        }

        try
        {
            using var acb = AcbFile.FromFile(filePath);
            var formatVersion = acb.FormatVersion;
            var awb = acb.InternalAwb ?? acb.ExternalAwb;

            using var awbStream = awb == acb.InternalAwb ? acb.Stream : File.OpenRead(awb.FileName);
            await ProcessAllBinaries(acb.FormatVersion, tempAwbFilePath, awb, awbStream);
            Log.LogInfo($"generate new: {tempAwbFilePath}");
            return tempAwbFilePath;
        }
        catch (Exception e)
        {
            Log.LogError($"Load acb file failed : {e.Message}");
            return null;
        }
    }
}