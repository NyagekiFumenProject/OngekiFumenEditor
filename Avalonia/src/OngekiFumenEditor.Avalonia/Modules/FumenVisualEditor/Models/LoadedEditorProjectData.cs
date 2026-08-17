#nullable enable

using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;

/// <summary>
/// Parsed project data that does not own the file-access context. This makes it possible
/// to validate newly-created files before deciding whether the file transaction should
/// transfer or roll back its capabilities.
/// </summary>
public sealed class LoadedEditorProjectData : IDisposable
{
    private OngekiFumen? fumen;

    public LoadedEditorProjectData(EditorProjectDataModel projectData, OngekiFumen fumen)
    {
        ProjectData = projectData ?? throw new ArgumentNullException(nameof(projectData));
        this.fumen = fumen ?? throw new ArgumentNullException(nameof(fumen));
    }

    public EditorProjectDataModel ProjectData { get; }

    public OngekiFumen Fumen => fumen ?? throw new ObjectDisposedException(nameof(LoadedEditorProjectData));

    public (EditorProjectDataModel ProjectData, OngekiFumen Fumen) Take()
    {
        var value = fumen ?? throw new ObjectDisposedException(nameof(LoadedEditorProjectData));
        fumen = null;
        return (ProjectData, value);
    }

    public void Dispose()
    {
        var value = Interlocked.Exchange(ref fumen, null);
        if (value is null)
            return;

        foreach (var svg in value.SvgPrefabs.ToArray())
            svg.Dispose();
    }
}
