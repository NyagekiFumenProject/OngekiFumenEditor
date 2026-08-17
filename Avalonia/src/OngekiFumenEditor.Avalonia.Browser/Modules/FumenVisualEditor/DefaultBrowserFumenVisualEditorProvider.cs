#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.FumenVisualEditor;

public sealed class DefaultBrowserFumenVisualEditorProvider : FumenVisualEditorProviderBase
{
    public override bool CanCreateNew => false;

    protected override IEditorProjectSetupFilePicker CreateSetupFilePicker() =>
        new AvaloniaEditorProjectSetupFilePicker(supportsAcb: false);

    protected override Task<EditorFileAccessContext> RestoreContextAsync(
        EditorFileAccessContextSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storageProvider = (Application.Current as global::OngekiFumenEditor.Avalonia.App)
            ?.TopLevel?.StorageProvider
            ?? throw new InvalidOperationException("No active Browser storage provider is available.");
        return snapshot.ToContextAsync(storageProvider);
    }
}
