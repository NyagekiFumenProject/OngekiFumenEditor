#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;

public sealed class EditorFileAccessContext : IDisposable
{
    private ISimpleDirectory? projectDirectory;
    private ISimpleFile? projectFile;
    private ISimpleFile? fumenFile;
    private ISimpleFile? audioFile;
    private ISimpleFile? audioAwbFile;
    private bool isDisposed;

    public ISimpleDirectory? ProjectDirectory
    {
        get => projectDirectory;
        set => Replace(ref projectDirectory, value);
    }

    public List<ISimpleDirectory> AdditionDirectories { get; set; } = [];

    public ISimpleFile? ProjectFile
    {
        get => projectFile;
        set => Replace(ref projectFile, value);
    }

    public ISimpleFile? FumenFile
    {
        get => fumenFile;
        set => Replace(ref fumenFile, value);
    }

    public ISimpleFile? AudioFile
    {
        get => audioFile;
        set => Replace(ref audioFile, value);
    }

    public ISimpleFile? AudioAwbFile
    {
        get => audioAwbFile;
        set => Replace(ref audioAwbFile, value);
    }

    public Task<EditorFileAccessContextSnapshot> ToSnapshotAsync() =>
        EditorFileAccessContextSnapshot.FromContextAsync(this);

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        var directories = GetOwnedDirectoryRoots();
        var standaloneFiles = DistinctByReference(
            new[] { projectFile, fumenFile, audioFile, audioAwbFile }
                .Where(file => file is not null && !IsOwnedByAnyDirectory(file, directories))
                .Cast<ISimpleFile>());

        for (var i = directories.Count - 1; i >= 0; i--)
            directories[i].Dispose();
        foreach (var file in standaloneFiles)
            file!.Dispose();

        projectDirectory = null;
        projectFile = null;
        fumenFile = null;
        audioFile = null;
        audioAwbFile = null;
        AdditionDirectories.Clear();
    }

    internal void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }

    private List<ISimpleDirectory> GetOwnedDirectoryRoots()
    {
        var candidates = new List<ISimpleDirectory>(AdditionDirectories.Count + 1);
        if (projectDirectory is not null)
            candidates.Add(projectDirectory);
        candidates.AddRange(AdditionDirectories.Where(directory => directory is not null));

        return DistinctByReference(candidates)
            .Where(candidate => !candidates.Any(other =>
                !ReferenceEquals(candidate, other) && IsDescendantOf(candidate, other)))
            .ToList();
    }

    private void Replace<T>(ref T? field, T? value) where T : class, IDisposable
    {
        ThrowIfDisposed();
        if (ReferenceEquals(field, value))
            return;

        field?.Dispose();
        field = value;
    }

    private static bool IsOwnedByAnyDirectory(
        ISimpleFile file,
        IReadOnlyCollection<ISimpleDirectory> directories)
    {
        for (var parent = file.ParentDictionary; parent is not null; parent = parent.ParentDictionary)
        {
            if (directories.Any(directory => ReferenceEquals(directory, parent)))
                return true;
        }

        return false;
    }

    private static bool IsDescendantOf(
        ISimpleDirectory candidate,
        ISimpleDirectory possibleAncestor)
    {
        for (var parent = candidate.ParentDictionary; parent is not null; parent = parent.ParentDictionary)
        {
            if (ReferenceEquals(parent, possibleAncestor))
                return true;
        }

        return false;
    }

    private static IEnumerable<T> DistinctByReference<T>(IEnumerable<T> items) where T : class
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var item in items)
        {
            if (seen.Add(item))
                yield return item;
        }
    }
}
