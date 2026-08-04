#nullable enable

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

[SupportedOSPlatform("browser")]
internal static partial class BrowserTemporaryFileSystemInterop
{
    [JSImport("globalThis.TemporaryFileSystemInterop.isAvailable")]
    public static partial bool IsAvailable();

    [JSImport("globalThis.TemporaryFileSystemInterop.getEntryKind")]
    public static partial Task<int> GetEntryKindAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.createFile")]
    public static partial Task CreateFileAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.tryCreateFile")]
    public static partial Task<bool> TryCreateFileAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.createFolder")]
    public static partial Task CreateFolderAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.tryCreateFolder")]
    public static partial Task<bool> TryCreateFolderAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.getFileLength")]
    public static partial Task<double> GetFileLengthAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.readFile")]
    public static partial Task<JSObject> ReadFileAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.setWriteBuffer")]
    public static partial void SetWriteBuffer(
        int handle,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> data,
        int byteLength);

    [JSImport("globalThis.TemporaryFileSystemInterop.releaseWriteBuffer")]
    public static partial void ReleaseWriteBuffer(int handle);

    [JSImport("globalThis.TemporaryFileSystemInterop.writeFile")]
    public static partial Task WriteFileAsync(string relativePath, int handle);

    [JSImport("globalThis.TemporaryFileSystemInterop.appendFile")]
    public static partial Task AppendFileAsync(string relativePath, int handle);

    [JSImport("globalThis.TemporaryFileSystemInterop.deleteFile")]
    public static partial Task DeleteFileAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.deleteFolder")]
    public static partial Task DeleteFolderAsync(string relativePath);

    [JSImport("globalThis.TemporaryFileSystemInterop.clear")]
    public static partial Task ClearAsync();
}
