#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;

public sealed class EditorFileAccessContext : IDisposable
{
    private ISimpleDirectory? projectDirectory;
    private IReadOnlyList<ISimpleDirectory> additionDirectories = [];
    private ISimpleFile? projectFile;
    private ISimpleFile? fumenFile;
    private ISimpleFile? audioFile;
    private ISimpleFile? audioAwbFile;
    // A role is an alias, but replacing a standalone role must not lose ownership of
    // the previous capability. Retain it until the context itself is disposed.
    private readonly List<ISimpleFile> detachedRoleFiles = [];
    private bool isDisposed;

    public ISimpleDirectory? ProjectDirectory
    {
        get => projectDirectory;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(projectDirectory, value))
                return;

            if (projectDirectory is not null && value is not null)
                throw new InvalidOperationException(
                    "The project directory cannot be replaced after the access context has been built.");

            ValidateDirectoryRoots(value, additionDirectories);
            projectDirectory = value;
        }
    }

    /// <summary>
    /// Additional directory roots owned by this context. The collection is copied and
    /// validated so an externally mutated list cannot introduce overlapping ownership.
    /// </summary>
    public IReadOnlyList<ISimpleDirectory> AdditionDirectories
    {
        get => additionDirectories;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            var copy = value.ToArray();
            ValidateDirectoryRoots(projectDirectory, copy);
            additionDirectories = copy;
        }
    }

    public ISimpleFile? ProjectFile
    {
        get => projectFile;
        set => ReplaceRole(ref projectFile, value);
    }

    public ISimpleFile? FumenFile
    {
        get => fumenFile;
        set => ReplaceRole(ref fumenFile, value);
    }

    public ISimpleFile? AudioFile
    {
        get => audioFile;
        set => ReplaceRole(ref audioFile, value);
    }

    public ISimpleFile? AudioAwbFile
    {
        get => audioAwbFile;
        set => ReplaceRole(ref audioAwbFile, value);
    }

    public static EditorFileAccessContext Create(
        ISimpleDirectory? projectDirectory = null,
        IEnumerable<ISimpleDirectory>? additionDirectories = null,
        ISimpleFile? projectFile = null,
        ISimpleFile? fumenFile = null,
        ISimpleFile? audioFile = null,
        ISimpleFile? audioAwbFile = null)
    {
        var context = new EditorFileAccessContext();
        try
        {
            // Assign roots before role aliases so all root validation happens before the
            // context can be observed by a consumer.
            context.ProjectDirectory = projectDirectory;
            context.AdditionDirectories = additionDirectories?.ToArray() ?? [];
            context.ProjectFile = projectFile;
            context.FumenFile = fumenFile;
            context.AudioFile = audioFile;
            context.AudioAwbFile = audioAwbFile;
            return context;
        }
        catch
        {
            context.Dispose();
            throw;
        }
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
                .Concat(detachedRoleFiles)
                .Where(file => file is not null && !IsOwnedByAnyDirectory(file, directories))
                .Cast<ISimpleFile>())
            .ToArray();

        List<Exception>? failures = null;
        try
        {
            for (var i = directories.Count - 1; i >= 0; i--)
            {
                try
                {
                    directories[i].Dispose();
                }
                catch (Exception exception)
                {
                    (failures ??= []).Add(exception);
                }
            }

            foreach (var file in standaloneFiles)
            {
                try
                {
                    file.Dispose();
                }
                catch (Exception exception)
                {
                    (failures ??= []).Add(exception);
                }
            }
        }
        finally
        {
            projectDirectory = null;
            projectFile = null;
            fumenFile = null;
            audioFile = null;
            audioAwbFile = null;
            additionDirectories = [];
            detachedRoleFiles.Clear();
        }

        if (failures is { Count: > 0 })
            throw new AggregateException("One or more editor file capabilities could not be released.", failures);
    }

    internal void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }

    private List<ISimpleDirectory> GetOwnedDirectoryRoots()
    {
        var candidates = new List<ISimpleDirectory>(additionDirectories.Count + 1);
        if (projectDirectory is not null)
            candidates.Add(projectDirectory);
        candidates.AddRange(additionDirectories);
        return DistinctByReference(candidates).ToList();
    }

    private void ReplaceRole(ref ISimpleFile? field, ISimpleFile? value)
    {
        ThrowIfDisposed();
        if (ReferenceEquals(field, value))
            return;

        // Role properties are aliases. A file may be owned by a directory root, or may
        // be referenced by more than one role; replacing one role must never dispose it.
        var previous = field;
        field = value;

        if (value is not null)
            RemoveDetachedRoleFile(value);

        if (previous is not null &&
            !IsReferencedByAnyRole(previous) &&
            !IsOwnedByAnyDirectory(previous, GetOwnedDirectoryRoots()))
        {
            AddDetachedRoleFile(previous);
        }
    }

    private bool IsReferencedByAnyRole(ISimpleFile file) =>
        ReferenceEquals(projectFile, file) ||
        ReferenceEquals(fumenFile, file) ||
        ReferenceEquals(audioFile, file) ||
        ReferenceEquals(audioAwbFile, file);

    private void AddDetachedRoleFile(ISimpleFile file)
    {
        if (!detachedRoleFiles.Any(existing => ReferenceEquals(existing, file)))
            detachedRoleFiles.Add(file);
    }

    private void RemoveDetachedRoleFile(ISimpleFile file)
    {
        for (var index = detachedRoleFiles.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(detachedRoleFiles[index], file))
                detachedRoleFiles.RemoveAt(index);
        }
    }

    private static void ValidateDirectoryRoots(
        ISimpleDirectory? projectDirectory,
        IReadOnlyList<ISimpleDirectory> additionDirectories)
    {
        var roots = new List<ISimpleDirectory>(additionDirectories.Count + 1);
        if (projectDirectory is not null)
            roots.Add(projectDirectory);
        roots.AddRange(additionDirectories);

        if (roots.Any(root => root is null))
            throw new ArgumentException("Directory roots cannot contain null entries.");

        var distinct = DistinctByReference(roots).ToArray();
        if (distinct.Length != roots.Count)
            throw new ArgumentException("Directory roots cannot contain the same capability twice.");

        for (var i = 0; i < distinct.Length; i++)
        {
            for (var j = i + 1; j < distinct.Length; j++)
            {
                if (IsDescendantOf(distinct[i], distinct[j]) ||
                    IsDescendantOf(distinct[j], distinct[i]) ||
                    AreOverlappingLocalPaths(distinct[i].FullPath, distinct[j].FullPath))
                {
                    throw new ArgumentException(
                        "Directory roots cannot overlap or contain one another.");
                }
            }
        }
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

    private static bool AreOverlappingLocalPaths(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            var leftPath = Path.GetFullPath(left);
            var rightPath = Path.GetFullPath(right);
            return IsSameOrDescendantPath(leftPath, rightPath) ||
                IsSameOrDescendantPath(rightPath, leftPath);
        }
        catch
        {
            // A virtual provider can expose opaque local-path-like identifiers. Object
            // ancestry remains authoritative when paths cannot be normalized safely.
            return false;
        }
    }

    private static bool IsSameOrDescendantPath(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative == "." ||
            (!Path.IsPathFullyQualified(relative) &&
             relative != ".." &&
             !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
             !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
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
