#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Platforms.Services.Logging;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Logging;

[RegisterSingleton<ILogFileStorage>]
public sealed class BrowserLogFileStorage : ILogFileStorage
{
    public const string LogDirectoryPathValue = "opfs:/logs";
    private static readonly SemaphoreSlim MutationGate = new(1, 1);
    private static int nextWriteBufferHandle;

    public BrowserLogFileStorage()
    {
        try
        {
            IsAvailable = BrowserLogFileSystemInterop.IsAvailable();
        }
        catch
        {
            IsAvailable = false;
        }
    }

    public bool IsAvailable { get; }

    public string LogDirectoryPath => LogDirectoryPathValue;

    public async Task<ILogFile?> CreateUniqueFileAsync(
        string prefix,
        string extension = ".log",
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return null;

        ValidateFileNamePart(prefix, nameof(prefix));
        ValidateFileNamePart(extension, nameof(extension));
        await MutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (int suffix = 0; ; suffix++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fileName = suffix == 0
                    ? $"{prefix}{extension}"
                    : $"{prefix}_{suffix}{extension}";
                if (await BrowserLogFileSystemInterop.TryCreateFileAsync(fileName).ConfigureAwait(false))
                    return new BrowserLogFile(fileName);
            }
        }
        finally
        {
            MutationGate.Release();
        }
    }

    private static void ValidateFileNamePart(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Contains('/') || value.Contains('\\') || value is "." or "..")
            throw new ArgumentException("Log file name parts cannot contain path separators.", parameterName);
    }

    private static async Task AppendAsync(
        string fileName,
        ReadOnlyMemory<byte> data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await MutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int handle = Interlocked.Increment(ref nextWriteBufferHandle);
            byte[] bytes = data.ToArray();
            try
            {
                BrowserLogFileSystemInterop.SetWriteBuffer(handle, bytes, bytes.Length);
                await BrowserLogFileSystemInterop.AppendFileAsync(fileName, handle).ConfigureAwait(false);
            }
            finally
            {
                BrowserLogFileSystemInterop.ReleaseWriteBuffer(handle);
            }
        }
        finally
        {
            MutationGate.Release();
        }
    }

    private sealed class BrowserLogFile(string fileName) : ILogFile
    {
        public string Path { get; } = $"{LogDirectoryPathValue}/{fileName}";

        public Task AppendAsync(
            ReadOnlyMemory<byte> data,
            CancellationToken cancellationToken = default) =>
            BrowserLogFileStorage.AppendAsync(fileName, data, cancellationToken);
    }
}
