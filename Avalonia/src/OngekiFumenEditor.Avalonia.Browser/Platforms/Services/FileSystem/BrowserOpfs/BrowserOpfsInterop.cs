#nullable enable

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.FileSystem.BrowserOpfs;

[SupportedOSPlatform("browser")]
internal static partial class BrowserOpfsInterop
{
    [JSImport("globalThis.BrowserOpfsInterop.isAvailable")]
    public static partial bool IsAvailable();

    [JSImport("globalThis.BrowserOpfsInterop.listDirectory")]
    public static partial Task<string> ListDirectoryAsync(string relativePath);

    [JSImport("globalThis.BrowserOpfsInterop.directoryExists")]
    public static partial Task<bool> DirectoryExistsAsync(string relativePath);

    [JSImport("globalThis.BrowserOpfsInterop.beginDownload")]
    public static partial Task<string> BeginDownloadAsync(string suggestedFileName, bool useZip);

    [JSImport("globalThis.BrowserOpfsInterop.buildManifest")]
    public static partial Task<string> BuildManifestAsync(string requestJson);

    [JSImport("globalThis.BrowserOpfsInterop.validateManifest")]
    public static partial Task<bool> ValidateManifestAsync(string manifestJson);

    [JSImport("globalThis.BrowserOpfsInterop.openRead")]
    public static partial Task<int> OpenReadAsync(
        string relativePath,
        double expectedSize,
        double expectedLastModified);

    [JSImport("globalThis.BrowserOpfsInterop.readChunk")]
    public static partial Task<JSObject> ReadChunkAsync(int handle, int maximumByteLength);

    [JSImport("globalThis.BrowserOpfsInterop.closeRead")]
    public static partial void CloseRead(int handle);

    [JSImport("globalThis.BrowserOpfsInterop.setWriteBuffer")]
    public static partial void SetWriteBuffer(
        int handle,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> data,
        int byteLength);

    [JSImport("globalThis.BrowserOpfsInterop.releaseWriteBuffer")]
    public static partial void ReleaseWriteBuffer(int handle);

    [JSImport("globalThis.BrowserOpfsInterop.queueDownloadBuffer")]
    public static partial void QueueDownloadBuffer(int outputHandle, int bufferHandle);

    [JSImport("globalThis.BrowserOpfsInterop.writeDownloadBuffer")]
    public static partial Task WriteDownloadBufferAsync(int outputHandle, int bufferHandle);

    [JSImport("globalThis.BrowserOpfsInterop.flushDownload")]
    public static partial Task FlushDownloadAsync(int outputHandle);

    [JSImport("globalThis.BrowserOpfsInterop.closeDownload")]
    public static partial Task CloseDownloadAsync(int outputHandle);

    [JSImport("globalThis.BrowserOpfsInterop.abortDownload")]
    public static partial Task AbortDownloadAsync(int outputHandle);
}
