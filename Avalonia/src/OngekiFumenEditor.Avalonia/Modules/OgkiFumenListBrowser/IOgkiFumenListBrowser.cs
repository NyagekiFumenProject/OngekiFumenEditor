#nullable enable

using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser;

public interface IOgkiFumenListBrowser
{
    WindowViewModelBase WindowViewModel { get; }

    Task<IReadOnlyList<OngekiFumenSet>> SearchFumenSet(
        ISimpleDirectory root,
        CancellationToken cancellationToken = default);
}
