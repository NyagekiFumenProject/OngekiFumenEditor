#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

public enum SetupFumenMode
{
    Existing,
    CreateNew
}

public enum SetupAudioPackageKind
{
    OrdinaryAudio,
    AcbWithInternalAwb,
    AcbWithExternalAwb
}

public sealed record FumenFormatOption(string DisplayName, string Extension)
{
    public override string ToString() => $"{DisplayName} ({Extension})";
}

public sealed class EditorProjectDirectorySelection : IDisposable
{
    private ISimpleDirectory? directory;

    public EditorProjectDirectorySelection(ISimpleDirectory directory, string? displayName)
    {
        this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
        DisplayName = displayName ?? string.Empty;
    }

    public ISimpleDirectory Directory => directory ?? throw new ObjectDisposedException(nameof(EditorProjectDirectorySelection));

    public string DisplayName { get; }

    internal ISimpleDirectory TakeDirectory() =>
        Interlocked.Exchange(ref directory, null)
            ?? throw new ObjectDisposedException(nameof(EditorProjectDirectorySelection));

    public void Dispose() => Interlocked.Exchange(ref directory, null)?.Dispose();
}

public interface IEditorProjectSetupFilePicker
{
    Task<EditorProjectDirectorySelection?> PickProjectDirectoryAsync(
        CancellationToken cancellationToken = default);

    Task<ISimpleFile?> PickAudioAsync(CancellationToken cancellationToken = default);

    Task<ISimpleFile?> PickExistingFumenAsync(CancellationToken cancellationToken = default);

    Task<ISimpleFile?> PickExternalAwbAsync(
        string expectedFileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A one-way ownership snapshot produced when the Setup form is confirmed.
/// </summary>
public sealed class EditorProjectSetupSelection : IDisposable
{
    private bool projectDirectoryTransferred;
    private bool sourceCapabilitiesDisposed;
    private bool disposed;

    public required ISimpleDirectory ProjectDirectory { get; init; }
    public required string ProjectDirectoryDisplayName { get; init; }
    public required string ProjectName { get; init; }
    public required string ProjectFileName { get; init; }
    public required SetupFumenMode FumenMode { get; init; }
    public required ISimpleFile AudioFile { get; init; }
    public ISimpleFile? AudioAwbFile { get; init; }
    public ISimpleFile? ExistingFumenFile { get; init; }
    public string? NewFumenFileName { get; init; }
    public double? BaseBpm { get; init; }
    public string? ExistingFumenTargetFileName { get; init; }
    public string? TargetAudioFileName { get; init; }
    public string? TargetAudioAwbFileName { get; init; }
    public TimeSpan AudioDuration { get; init; }
    public SetupAudioPackageKind AudioPackageKind { get; init; }
    public bool FumenRequiresImport { get; init; }
    public bool AudioRequiresImport { get; init; }
    public bool AudioAwbRequiresImport { get; init; }

    /// <summary>
    /// Additional roots retained while a user switches the selected project directory.
    /// They are source capabilities only and are never transferred to the editor.
    /// </summary>
    public IReadOnlyList<ISimpleDirectory> SourceDirectories { get; init; } = [];

    public EditorProjectSetupSelection Take() => this;

    internal void TransferProjectDirectory()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        projectDirectoryTransferred = true;
    }

    internal void DisposeSourceCapabilities()
    {
        if (sourceCapabilitiesDisposed)
            return;

        var roots = SourceDirectories
            .Where(root => root is not null)
            .DistinctBy(root => (object)root, ReferenceEqualityComparer.Instance)
            .ToArray();
        var files = GetDistinctRoleFiles()
            .Where(file => !roots.Any(root => IsOwnedByRoot(file, root)) &&
                !IsOwnedByRoot(file, ProjectDirectory))
            .ToArray();

        try
        {
            DisposeAll(
                roots.Reverse().Cast<IDisposable>()
                    .Concat(files));
            sourceCapabilitiesDisposed = true;
        }
        catch
        {
            // Allow a later owner cleanup attempt to retry capabilities whose Dispose
            // implementation failed transiently.
            sourceCapabilitiesDisposed = false;
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        // A transferred project root is still the owner of any role files selected from it.
        // Keep it in the ownership set even though it must no longer be disposed here.
        var ownershipRoots = new List<ISimpleDirectory> { ProjectDirectory };
        ownershipRoots.AddRange(SourceDirectories);
        var roots = new List<ISimpleDirectory>();
        if (!projectDirectoryTransferred)
            roots.Add(ProjectDirectory);
        if (!sourceCapabilitiesDisposed)
            roots.AddRange(SourceDirectories);

        var distinctRoots = roots
            .Where(root => root is not null)
            .DistinctBy(root => (object)root, ReferenceEqualityComparer.Instance)
            .ToArray();
        IEnumerable<ISimpleFile> files = sourceCapabilitiesDisposed
            ? []
            : GetDistinctRoleFiles()
                .Where(file => !ownershipRoots.Any(root => IsOwnedByRoot(file, root)))
                .ToArray();

        DisposeAll(
            distinctRoots.Reverse().Cast<IDisposable>()
                .Concat(files));
    }

    private IEnumerable<ISimpleFile> GetDistinctRoleFiles() =>
        new[] { ExistingFumenFile, AudioFile, AudioAwbFile }
            .Where(file => file is not null)
            .Cast<ISimpleFile>()
            .DistinctBy(file => (object)file, ReferenceEqualityComparer.Instance);

    private static void DisposeAll(IEnumerable<IDisposable> disposables)
    {
        List<Exception>? failures = null;
        foreach (var disposable in disposables)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is { Count: > 0 })
            throw new AggregateException("One or more project capabilities could not be released.", failures);
    }

    private static bool IsOwnedByRoot(ISimpleFile file, ISimpleDirectory root)
    {
        for (var parent = file.ParentDictionary; parent is not null; parent = parent.ParentDictionary)
        {
            if (ReferenceEquals(parent, root))
                return true;
        }

        return false;
    }
}
