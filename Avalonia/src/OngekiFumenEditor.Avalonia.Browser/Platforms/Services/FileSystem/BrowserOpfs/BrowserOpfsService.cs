#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.FileSystem.BrowserOpfs;

[SupportedOSPlatform("browser")]
[RegisterSingleton<IBrowserOpfsService>]
public sealed class BrowserOpfsService : IBrowserOpfsService
{
    private const int CopyBufferLength = 256 * 1024;
    private readonly ILogger<BrowserOpfsService> logger;

    public BrowserOpfsService(ILogger<BrowserOpfsService> logger)
    {
        this.logger = logger;
    }

    public bool IsAvailable
    {
        get
        {
            try
            {
                return BrowserOpfsInterop.IsAvailable();
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task<IReadOnlyList<BrowserOpfsEntrySnapshot>> ListDirectoryAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string json = await BrowserOpfsInterop.ListDirectoryAsync(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        BrowserOpfsEntryDto[] entries = JsonSerializer.Deserialize(
                                            json,
                                            BrowserOpfsJsonContext.Default.BrowserOpfsEntryDtoArray)
                                        ?? [];
        return entries.Select(ConvertEntry).ToArray();
    }

    public async Task<bool> DirectoryExistsAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool result = await BrowserOpfsInterop.DirectoryExistsAsync(relativePath);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public async Task<BrowserOpfsDownloadResult> DownloadAsync(
        IReadOnlyList<BrowserOpfsEntrySnapshot> selectedEntries,
        IProgress<BrowserOpfsDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BrowserOpfsDownloadPlan plan = BrowserOpfsDownloadPlanner.Create(selectedEntries, DateTimeOffset.Now);

        // This interop call must remain the first asynchronous operation so showSaveFilePicker runs
        // while the menu/button activation still counts as a browser user gesture.
        Task<string> beginDownloadTask = BrowserOpfsInterop.BeginDownloadAsync(
            plan.SuggestedFileName,
            plan.UseZip);
        string beginDownloadJson = await beginDownloadTask;
        BrowserOpfsBeginDownloadDto beginDownload = JsonSerializer.Deserialize(
            beginDownloadJson,
            BrowserOpfsJsonContext.Default.BrowserOpfsBeginDownloadDto)
            ?? throw new IOException("The browser returned no download destination information.");
        if (beginDownload.Canceled)
            return new BrowserOpfsDownloadResult(true);
        if (beginDownload.Handle <= 0)
            throw new IOException("The browser returned an invalid download output handle.");

        var output = new BrowserOpfsOutputStream(beginDownload.Handle);
        bool committed = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = new BrowserOpfsManifestRequestDto
            {
                SelectedEntries = plan.SelectedEntries.Select(x => new BrowserOpfsSelectionDto
                {
                    RelativePath = x.RelativePath,
                    Kind = (int)x.Kind,
                }).ToArray(),
            };
            string requestJson = JsonSerializer.Serialize(
                request,
                BrowserOpfsJsonContext.Default.BrowserOpfsManifestRequestDto);
            string manifestJson = await BrowserOpfsInterop.BuildManifestAsync(requestJson);
            BrowserOpfsManifestDto manifest = JsonSerializer.Deserialize(
                manifestJson,
                BrowserOpfsJsonContext.Default.BrowserOpfsManifestDto)
                ?? throw new IOException("The browser returned no OPFS download manifest.");

            progress?.Report(new BrowserOpfsDownloadProgress(
                string.Empty,
                0,
                manifest.TotalBytes,
                0,
                manifest.TotalFiles));

            if (plan.UseZip)
                await WriteZipAsync(manifest, output, progress, cancellationToken);
            else
                await WriteSingleFileAsync(manifest, output, progress, cancellationToken);

            await output.FlushAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!await BrowserOpfsInterop.ValidateManifestAsync(manifestJson))
                throw new IOException("The OPFS source changed while the download was being generated.");
            cancellationToken.ThrowIfCancellationRequested();

            await output.CommitAsync();
            committed = true;
            return new BrowserOpfsDownloadResult(false);
        }
        finally
        {
            if (!committed)
            {
                try
                {
                    await output.AbortAsync();
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Failed to clean a partial browser OPFS download output.");
                }
            }
        }
    }

    private static BrowserOpfsEntrySnapshot ConvertEntry(BrowserOpfsEntryDto entry)
    {
        BrowserOpfsEntryKind kind = entry.Kind switch
        {
            1 => BrowserOpfsEntryKind.File,
            2 => BrowserOpfsEntryKind.Folder,
            _ => throw new IOException($"OPFS returned unknown entry kind {entry.Kind} for '{entry.RelativePath}'.")
        };
        BrowserOpfsStagingState stagingState = entry.StagingState switch
        {
            0 => BrowserOpfsStagingState.None,
            1 => BrowserOpfsStagingState.GeneratingDownload,
            2 => BrowserOpfsStagingState.WaitingAutomaticCleanup,
            _ => throw new IOException(
                $"OPFS returned unknown staging state {entry.StagingState} for '{entry.RelativePath}'.")
        };
        return new BrowserOpfsEntrySnapshot(
            entry.Name,
            entry.RelativePath,
            kind,
            entry.Size,
            entry.LastModified,
            stagingState);
    }

    private static async Task WriteSingleFileAsync(
        BrowserOpfsManifestDto manifest,
        BrowserOpfsOutputStream output,
        IProgress<BrowserOpfsDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        BrowserOpfsManifestEntryDto[] files = manifest.Entries.Where(IsFile).ToArray();
        if (files.Length != 1 || manifest.Entries.Any(IsFolder))
            throw new IOException("A direct OPFS download must contain exactly one file.");

        long completedBytes = 0;
        await CopyFileAsync(
            files[0],
            output,
            manifest,
            progress,
            0,
            value => completedBytes += value,
            cancellationToken);
        progress?.Report(new BrowserOpfsDownloadProgress(
            files[0].Path,
            completedBytes,
            manifest.TotalBytes,
            1,
            manifest.TotalFiles));
    }

    private static async Task WriteZipAsync(
        BrowserOpfsManifestDto manifest,
        BrowserOpfsOutputStream output,
        IProgress<BrowserOpfsDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        long completedBytes = 0;
        int completedFiles = 0;
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (BrowserOpfsManifestEntryDto directory in manifest.Entries.Where(IsFolder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string entryName = NormalizeZipEntryPath(directory.Path, isDirectory: true);
                if (entryName.Length > 0)
                    archive.CreateEntry(entryName, CompressionLevel.Fastest);
            }

            foreach (BrowserOpfsManifestEntryDto file in manifest.Entries.Where(IsFile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string entryName = NormalizeZipEntryPath(file.Path, isDirectory: false);
                ZipArchiveEntry zipEntry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                using Stream destination = zipEntry.Open();
                await CopyFileAsync(
                    file,
                    destination,
                    manifest,
                    progress,
                    completedFiles,
                    value => completedBytes += value,
                    cancellationToken);
                completedFiles++;
                progress?.Report(new BrowserOpfsDownloadProgress(
                    file.Path,
                    completedBytes,
                    manifest.TotalBytes,
                    completedFiles,
                    manifest.TotalFiles));
            }
        }
    }

    private static async Task CopyFileAsync(
        BrowserOpfsManifestEntryDto file,
        Stream destination,
        BrowserOpfsManifestDto manifest,
        IProgress<BrowserOpfsDownloadProgress>? progress,
        int completedFiles,
        Func<long, long> reportWrittenBytes,
        CancellationToken cancellationToken)
    {
        await using BrowserOpfsReadStream source = await BrowserOpfsReadStream.OpenAsync(file, cancellationToken);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferLength);
        try
        {
            int readLength;
            while ((readLength = await source.ReadAsync(
                       buffer.AsMemory(0, CopyBufferLength),
                       cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, readLength), cancellationToken);
                long totalCompletedBytes = reportWrittenBytes(readLength);
                progress?.Report(new BrowserOpfsDownloadProgress(
                    file.Path,
                    Math.Min(manifest.TotalBytes, totalCompletedBytes),
                    manifest.TotalBytes,
                    completedFiles,
                    manifest.TotalFiles));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool IsFile(BrowserOpfsManifestEntryDto entry) => entry.Kind == 1;
    private static bool IsFolder(BrowserOpfsManifestEntryDto entry) => entry.Kind == 2;

    private static string NormalizeZipEntryPath(string relativePath, bool isDirectory)
    {
        string normalized = relativePath.Replace('\\', '/').Trim('/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".."))
            throw new IOException($"Unsafe OPFS ZIP entry path '{relativePath}'.");

        string result = string.Join('/', segments);
        if (isDirectory && result.Length > 0)
            result += "/";
        return result;
    }
}
