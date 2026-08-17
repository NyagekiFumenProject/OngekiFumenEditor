#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

public enum EditorProjectFileRole
{
    Fumen,
    Audio,
    AudioAwb,
    Project
}

public sealed record EditorProjectFileCopyPlan(
    EditorProjectFileRole Role,
    ISimpleFile SourceFile,
    string TargetFileName);

public sealed record EditorProjectExistingFileBinding(
    EditorProjectFileRole Role,
    ISimpleFile ProjectFile);

public sealed class EditorProjectCreationPlan : IDisposable
{
    private bool disposed;

    private EditorProjectCreationPlan(
        EditorProjectSetupSelection selection,
        IReadOnlyList<EditorProjectFileCopyPlan> filesToCopy,
        IReadOnlyList<EditorProjectExistingFileBinding> existingBindings,
        IReadOnlySet<string> plannedTargetFileNames)
    {
        Selection = selection;
        FilesToCopy = filesToCopy;
        ExistingBindings = existingBindings;
        PlannedTargetFileNames = plannedTargetFileNames;
    }

    public EditorProjectSetupSelection Selection { get; }
    public IReadOnlyList<EditorProjectFileCopyPlan> FilesToCopy { get; }
    public IReadOnlyList<EditorProjectExistingFileBinding> ExistingBindings { get; }
    public IReadOnlySet<string> PlannedTargetFileNames { get; }

    public static EditorProjectCreationPlan Create(EditorProjectSetupSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        PortableEntryNameValidator.ThrowIfInvalid(selection.ProjectFileName, nameof(selection.ProjectFileName));
        if (!selection.ProjectFileName.EndsWith(
                FumenVisualEditorProviderBase.FILE_EXTENSION_NAME,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The project file must use the '{FumenVisualEditorProviderBase.FILE_EXTENSION_NAME}' extension.",
                nameof(selection.ProjectFileName));
        }

        var root = selection.ProjectDirectory;
        var copyPlans = new List<EditorProjectFileCopyPlan>();
        var existingBindings = new List<EditorProjectExistingFileBinding>();

        var fumen = selection.FumenMode == SetupFumenMode.Existing
            ? selection.ExistingFumenFile ?? throw new InvalidOperationException("An existing fumen is required.")
            : null;
        if (fumen is not null)
        {
            var targetName = selection.ExistingFumenTargetFileName ?? fumen.FileName;
            PortableEntryNameValidator.ThrowIfInvalid(targetName, nameof(selection.ExistingFumenTargetFileName));
            if (selection.FumenRequiresImport ||
                !EditorProjectSetupValidation.IsFileOwnedByDirectory(fumen, root) ||
                !fumen.FileName.Equals(targetName, StringComparison.Ordinal))
                copyPlans.Add(new(EditorProjectFileRole.Fumen, fumen, targetName));
            else
                existingBindings.Add(new(EditorProjectFileRole.Fumen, fumen));
        }

        var audioTargetName = selection.TargetAudioFileName ?? selection.AudioFile.FileName;
        PortableEntryNameValidator.ThrowIfInvalid(audioTargetName, nameof(selection.TargetAudioFileName));
        if (selection.AudioRequiresImport ||
            !EditorProjectSetupValidation.IsFileOwnedByDirectory(selection.AudioFile, root) ||
            !selection.AudioFile.FileName.Equals(audioTargetName, StringComparison.Ordinal))
            copyPlans.Add(new(EditorProjectFileRole.Audio, selection.AudioFile, audioTargetName));
        else
            existingBindings.Add(new(EditorProjectFileRole.Audio, selection.AudioFile));

        if (selection.AudioAwbFile is not null)
        {
            var awbTargetName = selection.TargetAudioAwbFileName ?? selection.AudioAwbFile.FileName;
            PortableEntryNameValidator.ThrowIfInvalid(awbTargetName, nameof(selection.TargetAudioAwbFileName));
            if (selection.AudioAwbRequiresImport ||
                !EditorProjectSetupValidation.IsFileOwnedByDirectory(selection.AudioAwbFile, root) ||
                !selection.AudioAwbFile.FileName.Equals(awbTargetName, StringComparison.Ordinal))
                copyPlans.Add(new(EditorProjectFileRole.AudioAwb, selection.AudioAwbFile, awbTargetName));
            else
                existingBindings.Add(new(EditorProjectFileRole.AudioAwb, selection.AudioAwbFile));
        }

        var targets = copyPlans.Select(plan => plan.TargetFileName).ToList();
        if (selection.FumenMode == SetupFumenMode.CreateNew)
        {
            var newFumenFileName = selection.NewFumenFileName ??
                throw new InvalidOperationException("A new fumen name is required.");
            PortableEntryNameValidator.ThrowIfInvalid(newFumenFileName, nameof(selection.NewFumenFileName));
            targets.Add(newFumenFileName);
        }
        targets.Add(selection.ProjectFileName);

        var duplicate = targets
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"The creation plan contains duplicate target '{duplicate.Key}'.");

        return new EditorProjectCreationPlan(
            selection,
            copyPlans.ToArray(),
            existingBindings.ToArray(),
            targets.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        Selection.Dispose();
    }
}
