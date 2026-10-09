using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
            // The old basename-only key made two different ACB files named, for
            // example, "music.acb" share one decoded WAV.  Include the canonical
            // source path and basic file identity in the cache key so a cache hit
            // always belongs to this version of this input file.
            var fullPath = Path.GetFullPath(filePath);
            long fileLength = 0;
            long lastWriteTicks = 0;
            try
            {
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Exists)
                {
                    fileLength = fileInfo.Length;
                    lastWriteTicks = fileInfo.LastWriteTimeUtc.Ticks;
                }
            }
            catch
            {
                // AcbFile.FromFile below reports the actual input error.
            }

            var cacheIdentity = $"{fullPath}\0{fileLength}\0{lastWriteTicks}";
            var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheIdentity)));
            tempAwbFilePath = Path.Combine(tempFolder, $"{Path.GetFileNameWithoutExtension(filePath)}.{cacheKey}.wav");
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
