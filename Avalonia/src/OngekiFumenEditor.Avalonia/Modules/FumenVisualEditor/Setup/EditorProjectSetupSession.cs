#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

/// <summary>
/// Owns picker capabilities while the Setup form is editable. Directory changes are
/// intentionally one-way: old roots remain available as source capabilities until the
/// selection is consumed, so an already-selected file is never invalidated mid-form.
/// </summary>
public sealed class EditorProjectSetupSession : IDisposable
{
    private ISimpleDirectory? projectDirectory;
    private string projectDirectoryDisplayName;
    private readonly List<ISimpleDirectory> sourceDirectories = [];
    private ISimpleFile? audioFile;
    private ISimpleFile? audioAwbFile;
    private ISimpleFile? existingFumenFile;
    private bool consumed;
    private bool disposed;

    public EditorProjectSetupSession(
        EditorProjectDirectorySelection initialDirectory,
        IEditorProjectSetupFilePicker filePicker)
    {
        ArgumentNullException.ThrowIfNull(initialDirectory);
        FilePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        projectDirectory = initialDirectory.TakeDirectory();
        projectDirectoryDisplayName = initialDirectory.DisplayName;
    }

    public IEditorProjectSetupFilePicker FilePicker { get; }

    public ISimpleDirectory ProjectDirectory => projectDirectory
        ?? throw new ObjectDisposedException(nameof(EditorProjectSetupSession));

    public string ProjectDirectoryDisplayName => projectDirectoryDisplayName;

    public ISimpleFile? AudioFile => audioFile;

    public ISimpleFile? AudioAwbFile => audioAwbFile;

    public ISimpleFile? ExistingFumenFile => existingFumenFile;

    public IReadOnlyList<ISimpleDirectory> SourceDirectories => sourceDirectories;

    public void SetProjectDirectory(EditorProjectDirectorySelection selection)
    {
        EnsureEditable();
        ArgumentNullException.ThrowIfNull(selection);

        var replacement = selection.TakeDirectory();
        if (ReferenceEquals(replacement, projectDirectory))
        {
            // The session already owns this capability. The picker selection is only
            // another alias, so disposing it here would invalidate the active root.
            return;
        }

        if ((projectDirectory is not null && AreOverlappingRoots(projectDirectory, replacement)) ||
            sourceDirectories.Any(root => AreOverlappingRoots(root, replacement)))
        {
            replacement.Dispose();
            throw new InvalidOperationException(
                "The selected project folder overlaps a folder already retained by Setup.");
        }

        if (projectDirectory is not null)
            sourceDirectories.Add(projectDirectory);
        projectDirectory = replacement;
        projectDirectoryDisplayName = selection.DisplayName;
    }

    public void SetAudioFile(ISimpleFile? file)
    {
        EnsureEditable();
        ReplaceFile(ref audioFile, file);
        if (audioAwbFile is not null)
            ReplaceFile(ref audioAwbFile, null);
    }

    public void SetAudioAwbFile(ISimpleFile? file)
    {
        EnsureEditable();
        ReplaceFile(ref audioAwbFile, file);
    }

    public void SetExistingFumenFile(ISimpleFile? file)
    {
        EnsureEditable();
        ReplaceFile(ref existingFumenFile, file);
    }

    public EditorProjectSetupSelection TakeSelection(
        string projectName,
        string projectFileName,
        SetupFumenMode fumenMode,
        string? newFumenFileName,
        double? baseBpm,
        string? existingFumenTargetFileName,
        string? targetAudioFileName,
        string? targetAudioAwbFileName,
        TimeSpan audioDuration,
        SetupAudioPackageKind audioPackageKind,
        bool fumenRequiresImport,
        bool audioRequiresImport,
        bool audioAwbRequiresImport)
    {
        EnsureEditable();
        if (projectDirectory is null || audioFile is null)
            throw new InvalidOperationException("A project directory and audio file are required.");

        var selection = new EditorProjectSetupSelection
        {
            ProjectDirectory = projectDirectory,
            ProjectDirectoryDisplayName = projectDirectoryDisplayName,
            ProjectName = projectName,
            ProjectFileName = projectFileName,
            FumenMode = fumenMode,
            AudioFile = audioFile,
            AudioAwbFile = audioAwbFile,
            ExistingFumenFile = existingFumenFile,
            NewFumenFileName = newFumenFileName,
            BaseBpm = baseBpm,
            ExistingFumenTargetFileName = existingFumenTargetFileName,
            TargetAudioFileName = targetAudioFileName,
            TargetAudioAwbFileName = targetAudioAwbFileName,
            AudioDuration = audioDuration,
            AudioPackageKind = audioPackageKind,
            SupportsAcb = FilePicker.SupportsAcb,
            FumenRequiresImport = fumenRequiresImport,
            AudioRequiresImport = audioRequiresImport,
            AudioAwbRequiresImport = audioAwbRequiresImport,
            SourceDirectories = sourceDirectories.ToArray()
        };

        // The selection now owns every capability. The session must not dispose or mutate
        // any of them after this point; the transaction decides commit vs rollback.
        consumed = true;
        projectDirectory = null;
        audioFile = null;
        audioAwbFile = null;
        existingFumenFile = null;
        sourceDirectories.Clear();
        return selection.Take();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        if (consumed)
            return;

        var roots = sourceDirectories.ToArray();
        if (projectDirectory is not null)
            roots = [.. roots, projectDirectory];
        var distinctRoots = roots
            .DistinctBy(item => (object)item, ReferenceEqualityComparer.Instance)
            .ToArray();
        var standaloneFiles = new[] { audioFile, audioAwbFile, existingFumenFile }
            .Where(file => file is not null)
            .Cast<ISimpleFile>()
            .DistinctBy(item => (object)item, ReferenceEqualityComparer.Instance)
            .Where(file => !distinctRoots.Any(root =>
                EditorProjectSetupValidation.IsFileOwnedByDirectory(file, root)))
            .ToArray();
        List<Exception>? failures = null;
        try
        {
            foreach (var root in distinctRoots.Reverse())
            {
                try
                {
                    root.Dispose();
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
            audioFile = null;
            audioAwbFile = null;
            existingFumenFile = null;
            sourceDirectories.Clear();
        }

        if (failures is { Count: > 0 })
            throw new AggregateException("One or more setup capabilities could not be released.", failures);
    }

    private void ReplaceFile(ref ISimpleFile? field, ISimpleFile? replacement)
    {
        if (ReferenceEquals(field, replacement))
            return;
        var previous = field;
        field = replacement;
        if (previous is not null && !IsReferencedByAnotherRole(previous) && !IsOwnedByAnyRoot(previous))
            previous.Dispose();
    }

    private bool IsReferencedByAnotherRole(ISimpleFile file) =>
        ReferenceEquals(audioFile, file) ||
        ReferenceEquals(audioAwbFile, file) ||
        ReferenceEquals(existingFumenFile, file);

    private bool IsOwnedByAnyRoot(ISimpleFile file)
    {
        return (projectDirectory is not null && EditorProjectSetupValidation.IsFileOwnedByDirectory(file, projectDirectory)) ||
            sourceDirectories.Any(root => EditorProjectSetupValidation.IsFileOwnedByDirectory(file, root));
    }

    private static bool AreOverlappingRoots(ISimpleDirectory left, ISimpleDirectory right) =>
        ReferenceEquals(left, right) ||
        IsDescendantOf(left, right) ||
        IsDescendantOf(right, left) ||
        AreOverlappingLocalPaths(left.LocalPath, right.LocalPath);

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

    private void EnsureEditable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (consumed)
            throw new InvalidOperationException("The setup session has already been consumed.");
    }
}
