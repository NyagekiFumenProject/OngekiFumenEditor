#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Services;

public interface IOgkiFumenListBrowserJacketDecoder
{
    Task<byte[]?> LoadPngBytesAsync(
        ISimpleFile sourceFile,
        CancellationToken cancellationToken = default);
}
