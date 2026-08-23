#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

namespace OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor;

public sealed class DefaultDesktopFumenVisualEditorProvider : FumenVisualEditorProviderBase
{
    public override bool CanCreateNew => true;

    protected override IEditorProjectSetupFilePicker CreateSetupFilePicker() =>
        new AvaloniaEditorProjectSetupFilePicker();

    protected override Task<EditorFileAccessContext> RestoreContextAsync(
        EditorFileAccessContextSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storageProvider = (Application.Current as global::OngekiFumenEditor.Avalonia.App)
            ?.TopLevel?.StorageProvider
            ?? throw new InvalidOperationException("No active Desktop storage provider is available.");
        return snapshot.ToContextAsync(storageProvider);
    }
}
