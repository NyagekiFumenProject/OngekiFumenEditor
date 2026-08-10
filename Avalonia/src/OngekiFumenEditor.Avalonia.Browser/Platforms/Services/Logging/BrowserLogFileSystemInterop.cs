#nullable enable

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Logging;

[SupportedOSPlatform("browser")]
internal static partial class BrowserLogFileSystemInterop
{
    [JSImport("globalThis.LogFileSystemInterop.isAvailable")]
    public static partial bool IsAvailable();

    [JSImport("globalThis.LogFileSystemInterop.tryCreateFile")]
    public static partial Task<bool> TryCreateFileAsync(string fileName);

    [JSImport("globalThis.LogFileSystemInterop.setWriteBuffer")]
    public static partial void SetWriteBuffer(
        int handle,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> data,
        int byteLength);

    [JSImport("globalThis.LogFileSystemInterop.releaseWriteBuffer")]
    public static partial void ReleaseWriteBuffer(int handle);

    [JSImport("globalThis.LogFileSystemInterop.appendFile")]
    public static partial Task AppendFileAsync(string fileName, int handle);
}
