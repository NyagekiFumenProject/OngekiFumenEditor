#nullable enable

using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;

public enum EditorProjectSetupRunState
{
    Editing,
    Running,
    CancellationRequested,
    Finalizing,
    RollingBack,
    Completed
}

public partial class EditorProjectSetupDialogViewModel : WindowViewModelBase, IDisposable
{
    private readonly EditorProjectSetupSession session;
    private readonly IFumenParserManager parserManager;
    private readonly IAudioManager audioManager;
    private readonly EditorProjectCreationCoordinator coordinator;
    private readonly Func<string, Task> showMessageAsync;
    private readonly object stateSync = new();
    private CancellationTokenSource? creationCancellation;
    private string projectDirectoryDisplayName;
    private string projectName;
    private SetupFumenMode fumenMode = SetupFumenMode.CreateNew;
    private string newFumenStem;
    private FumenFormatOption? selectedFumenFormat;
    private string baseBpmText = "240";
    private string existingFumenTargetFileName = string.Empty;
    private string targetAudioFileName = string.Empty;
    private string targetAudioAwbFileName = string.Empty;
    private TimeSpan audioDuration;
    private SetupAudioPackageKind audioPackageKind;
    private string validationMessage = string.Empty;
    private EditorProjectSetupRunState runState;
    private string progressText = string.Empty;
    private string progressFileName = string.Empty;
    private double progressValue;
    private double progressMaximum = 1;
    private bool isProgressIndeterminate = true;
    private bool projectNameWasEdited;
    private bool fumenNameWasEdited;
    private bool disposed;

    public EditorProjectSetupDialogViewModel(
        EditorProjectSetupSession session,
        IFumenParserManager parserManager,
        IAudioManager audioManager,
        EditorProjectCreationCoordinator coordinator,
        Func<string, Task> showMessageAsync)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.parserManager = parserManager ?? throw new ArgumentNullException(nameof(parserManager));
        this.audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.showMessageAsync = showMessageAsync ?? throw new ArgumentNullException(nameof(showMessageAsync));

        projectDirectoryDisplayName = session.ProjectDirectoryDisplayName;
        projectName = SuggestProjectName(projectDirectoryDisplayName);
        newFumenStem = projectName;
        FumenFormatOptions = EditorProjectSetupValidation.GetFumenFormatOptions(parserManager);
        selectedFumenFormat = FumenFormatOptions.FirstOrDefault(option =>
            option.Extension.Equals(".ogkr", StringComparison.OrdinalIgnoreCase)) ??
            FumenFormatOptions.FirstOrDefault();
        Revalidate();
    }

    public IReadOnlyList<FumenFormatOption> FumenFormatOptions { get; }

    public string ProjectDirectoryDisplayName
    {
        get => projectDirectoryDisplayName;
        private set => SetProperty(ref projectDirectoryDisplayName, value);
    }

    public string ProjectName
    {
        get => projectName;
        set
        {
            if (!SetProperty(ref projectName, value))
                return;
            projectNameWasEdited = true;
            OnPropertyChanged(nameof(ProjectFileNamePreview));
            if (!fumenNameWasEdited)
            {
                newFumenStem = value;
                OnPropertyChanged(nameof(NewFumenStem));
                OnPropertyChanged(nameof(FumenFileNamePreview));
            }
            Revalidate();
        }
    }

    public string ProjectFileNamePreview =>
        string.IsNullOrWhiteSpace(ProjectName)
            ? string.Empty
            : ProjectName + FumenVisualEditorProviderBase.FILE_EXTENSION_NAME;

    public SetupFumenMode FumenMode
    {
        get => fumenMode;
        set
        {
            if (!SetProperty(ref fumenMode, value))
                return;
            OnPropertyChanged(nameof(IsExistingFumenMode));
            OnPropertyChanged(nameof(IsCreateNewFumenMode));
            Revalidate();
        }
    }

    public bool IsExistingFumenMode
    {
        get => FumenMode == SetupFumenMode.Existing;
        set
        {
            if (value)
                FumenMode = SetupFumenMode.Existing;
        }
    }

    public bool IsCreateNewFumenMode
    {
        get => FumenMode == SetupFumenMode.CreateNew;
        set
        {
            if (value)
                FumenMode = SetupFumenMode.CreateNew;
        }
    }

    public string NewFumenStem
    {
        get => newFumenStem;
        set
        {
            if (!SetProperty(ref newFumenStem, value))
                return;
            fumenNameWasEdited = true;
            OnPropertyChanged(nameof(FumenFileNamePreview));
            Revalidate();
        }
    }

    public FumenFormatOption? SelectedFumenFormat
    {
        get => selectedFumenFormat;
        set
        {
            if (!SetProperty(ref selectedFumenFormat, value))
                return;
            OnPropertyChanged(nameof(FumenFileNamePreview));
            Revalidate();
        }
    }

    public string FumenFileNamePreview =>
        string.IsNullOrWhiteSpace(NewFumenStem) || SelectedFumenFormat is null
            ? string.Empty
            : NewFumenStem + SelectedFumenFormat.Extension;

    public string BaseBpmText
    {
        get => baseBpmText;
        set
        {
            if (SetProperty(ref baseBpmText, value))
                Revalidate();
        }
    }

    public string ExistingFumenDisplayName => GetDisplayPath(session.ExistingFumenFile);

    public string ExistingFumenTargetFileName
    {
        get => existingFumenTargetFileName;
        set
        {
            if (SetProperty(ref existingFumenTargetFileName, value))
                Revalidate();
        }
    }

    public string AudioFileDisplayName => GetDisplayPath(session.AudioFile);

    public string TargetAudioFileName
    {
        get => targetAudioFileName;
        set
        {
            if (SetProperty(ref targetAudioFileName, value))
                Revalidate();
        }
    }

    public string AudioAwbDisplayName => GetDisplayPath(session.AudioAwbFile);

    public string TargetAudioAwbFileName
    {
        get => targetAudioAwbFileName;
        private set
        {
            if (SetProperty(ref targetAudioAwbFileName, value))
                Revalidate();
        }
    }

    public bool RequiresExternalAwb => audioPackageKind == SetupAudioPackageKind.AcbWithExternalAwb;

    public string AudioDurationDisplay => audioDuration == default
        ? string.Empty
        : audioDuration.ToString(@"hh\:mm\:ss\.fff");

    public EditorProjectSetupRunState RunState
    {
        get => runState;
        private set
        {
            if (!SetProperty(ref runState, value))
                return;
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanCancel));
            Revalidate();
        }
    }

    public bool IsEditing => RunState == EditorProjectSetupRunState.Editing;

    public bool IsBusy => RunState is not EditorProjectSetupRunState.Editing and
        not EditorProjectSetupRunState.Completed;

    public bool CanCancel => RunState is EditorProjectSetupRunState.Editing or
        EditorProjectSetupRunState.Running;

    public string ValidationMessage
    {
        get => validationMessage;
        private set
        {
            if (!SetProperty(ref validationMessage, value))
                return;
            OnPropertyChanged(nameof(HasValidationErrors));
            OnPropertyChanged(nameof(CanCreate));
        }
    }

    public bool HasValidationErrors => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool CanCreate => IsEditing && !HasValidationErrors;

    public string ProgressText
    {
        get => progressText;
        private set => SetProperty(ref progressText, value);
    }

    public string ProgressFileName
    {
        get => progressFileName;
        private set => SetProperty(ref progressFileName, value);
    }

    public double ProgressValue
    {
        get => progressValue;
        private set => SetProperty(ref progressValue, value);
    }

    public double ProgressMaximum
    {
        get => progressMaximum;
        private set => SetProperty(ref progressMaximum, value);
    }

    public bool IsProgressIndeterminate
    {
        get => isProgressIndeterminate;
        private set => SetProperty(ref isProgressIndeterminate, value);
    }

    [RelayCommand]
    private async Task SelectProjectDirectoryAsync()
    {
        Log.LogInfo("Select project directory triggered.");
        if (!IsEditing)
            return;
        try
        {
            using var selection = await session.FilePicker.PickProjectDirectoryAsync();
            if (selection is null)
                return;
            session.SetProjectDirectory(selection);
            ProjectDirectoryDisplayName = session.ProjectDirectoryDisplayName;
            if (!projectNameWasEdited)
            {
                projectName = SuggestProjectName(ProjectDirectoryDisplayName);
                OnPropertyChanged(nameof(ProjectName));
                OnPropertyChanged(nameof(ProjectFileNamePreview));
                if (!fumenNameWasEdited)
                {
                    newFumenStem = projectName;
                    OnPropertyChanged(nameof(NewFumenStem));
                    OnPropertyChanged(nameof(FumenFileNamePreview));
                }
            }
            Revalidate();
        }
        catch (Exception exception)
        {
            ValidationMessage = $"Unable to use the selected folder: {exception.Message}";
        }
    }

    [RelayCommand]
    private async Task SelectAudioFileAsync()
    {
        Log.LogInfo("Select audio file triggered.");
        if (!IsEditing)
            return;
        ISimpleFile? selectedAudio = null;
        ISimpleFile? selectedAwb = null;
        string? expectedAwbTargetName = null;
        try
        {
            selectedAudio = await session.FilePicker.PickAudioAsync();
            if (selectedAudio is null)
                return;
            selectedAudio = ResolveProjectOwnedCapability(selectedAudio);

            var inspection = await AcbPackageInspector.InspectAsync(selectedAudio, selectedAwb);
            if (!inspection.IsValid)
                throw new InvalidDataException(inspection.ErrorMessage);

            if (inspection.Kind == SetupAudioPackageKind.AcbWithExternalAwb)
            {
                expectedAwbTargetName = inspection.RequiredExternalAwbLeafName!;
                selectedAwb = FindSibling(selectedAudio, expectedAwbTargetName);
                if (selectedAwb is null)
                {
                    selectedAwb = await session.FilePicker.PickExternalAwbAsync(
                        expectedAwbTargetName);
                    if (selectedAwb is null)
                        return;
                    selectedAwb = ResolveProjectOwnedCapability(selectedAwb);
                }
            }

            await using var audioStream = await selectedAudio.OpenRead();
            using var player = selectedAwb is null
                ? await audioManager.LoadAudioAsync(
                    audioStream)
                : await LoadAudioWithExternalAwbAsync(
                    audioManager,
                    selectedAwb,
                    audioStream);

            session.SetAudioFile(selectedAudio);
            selectedAudio = null;
            session.SetAudioAwbFile(selectedAwb);
            selectedAwb = null;
            audioPackageKind = inspection.Kind;
            audioDuration = player.Duration;
            targetAudioFileName = session.AudioFile!.FileName;
            targetAudioAwbFileName = expectedAwbTargetName ?? string.Empty;
            NotifyAudioChanged();
            Revalidate();
        }
        catch (Exception exception)
        {
            ValidationMessage = $"Unable to use the selected audio: {exception.Message}";
        }
        finally
        {
            DisposeIfStandalone(selectedAwb);
            DisposeIfStandalone(selectedAudio);
        }
    }

    private static async Task<IAudioPlayer> LoadAudioWithExternalAwbAsync(
        IAudioManager audioManager,
        ISimpleFile externalAwbFile,
        Stream audioStream)
    {
        await using var externalAwbStream = await externalAwbFile.OpenRead();
        return await audioManager.LoadAudioAsync(
            audioStream,
            externalAwbStream);
    }

    [RelayCommand]
    private void ClearAudioFile()
    {
        Log.LogInfo("Clear audio file triggered.");
        if (!IsEditing)
            return;
        session.SetAudioFile(null);
        audioPackageKind = SetupAudioPackageKind.OrdinaryAudio;
        audioDuration = default;
        targetAudioFileName = string.Empty;
        targetAudioAwbFileName = string.Empty;
        NotifyAudioChanged();
        Revalidate();
    }

    [RelayCommand]
    private async Task SelectExistingFumenAsync()
    {
        Log.LogInfo("Select existing fumen triggered.");
        if (!IsEditing)
            return;
        ISimpleFile? selected = null;
        try
        {
            selected = await session.FilePicker.PickExistingFumenAsync();
            if (selected is null)
                return;
            selected = ResolveProjectOwnedCapability(selected);
            var deserializer = parserManager.GetDeserializer(selected.FileName) ??
                throw new NotSupportedException($"No fumen parser supports '{selected.FileName}'.");
            await using (var stream = await selected.OpenRead())
            {
                var fumen = await deserializer.DeserializeAsync(stream);
                foreach (var svg in fumen.SvgPrefabs.ToArray())
                    svg.Dispose();
            }

            session.SetExistingFumenFile(selected);
            selected = null;
            existingFumenTargetFileName = session.ExistingFumenFile!.FileName;
            OnPropertyChanged(nameof(ExistingFumenDisplayName));
            OnPropertyChanged(nameof(ExistingFumenTargetFileName));
            Revalidate();
        }
        catch (Exception exception)
        {
            ValidationMessage = $"Unable to use the selected fumen: {exception.Message}";
        }
        finally
        {
            DisposeIfStandalone(selected);
        }
    }

    [RelayCommand]
    private void ClearExistingFumen()
    {
        Log.LogInfo("Clear existing fumen triggered.");
        if (!IsEditing)
            return;
        session.SetExistingFumenFile(null);
        existingFumenTargetFileName = string.Empty;
        OnPropertyChanged(nameof(ExistingFumenDisplayName));
        OnPropertyChanged(nameof(ExistingFumenTargetFileName));
        Revalidate();
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Log.LogInfo("Create project triggered.");
        if (!CanCreate)
            return;

        EditorProjectCreationPlan? plan = null;
        try
        {
            if (!TryBuildSelection(out var selection, out var error))
            {
                ValidationMessage = error;
                return;
            }

            try
            {
                plan = EditorProjectCreationPlan.Create(selection!);
            }
            catch
            {
                selection!.Dispose();
                throw;
            }
            creationCancellation = new CancellationTokenSource();
            RunState = EditorProjectSetupRunState.Running;
            var progress = new Progress<EditorProjectCreationProgress>(UpdateProgress);
            var outcome = await coordinator.RunAsync(
                plan,
                progress,
                creationCancellation.Token,
                TryBeginFinalizing,
                BeginRollback);
            plan = null;

            switch (outcome)
            {
                case EditorProjectCreationOutcome.Succeeded:
                    RunState = EditorProjectSetupRunState.Completed;
                    await TryCloseAsync(true);
                    break;
                case EditorProjectCreationOutcome.Canceled canceled:
                    RunState = EditorProjectSetupRunState.Completed;
                    if (canceled.RollbackFailures.Count > 0)
                        await showMessageAsync(BuildRollbackFailureMessage(canceled.RollbackFailures));
                    await TryCloseAsync(false);
                    break;
                case EditorProjectCreationOutcome.Failed failed:
                    RunState = EditorProjectSetupRunState.Completed;
                    var message = failed.Exception.Message;
                    if (failed.RollbackFailures.Count > 0)
                        message += Environment.NewLine + BuildRollbackFailureMessage(failed.RollbackFailures);
                    await showMessageAsync(message);
                    await TryCloseAsync(false);
                    break;
            }
        }
        catch (Exception exception)
        {
            plan?.Dispose();
            RunState = EditorProjectSetupRunState.Completed;
            await showMessageAsync($"Unable to start project creation: {exception.Message}");
            await TryCloseAsync(false);
        }
        finally
        {
            creationCancellation?.Dispose();
            creationCancellation = null;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        Log.LogInfo("Cancel project setup triggered.");
        if (RunState == EditorProjectSetupRunState.Editing)
        {
            RunState = EditorProjectSetupRunState.Completed;
            await TryCloseAsync(false);
            return;
        }

        RequestCancellation();
    }

    public bool HandleWindowClosing()
    {
        if (RunState is EditorProjectSetupRunState.Editing or EditorProjectSetupRunState.Completed)
            return false;
        RequestCancellation();
        return true;
    }

    public override void OnViewBeforeUnload(IView view)
    {
        Dispose();
        base.OnViewBeforeUnload(view);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        creationCancellation?.Cancel();
        creationCancellation?.Dispose();
        creationCancellation = null;
        session.Dispose();
    }

    private bool TryBuildSelection(
        out EditorProjectSetupSelection? selection,
        out string error)
    {
        selection = null;
        Revalidate();
        if (HasValidationErrors)
        {
            error = ValidationMessage;
            return false;
        }

        var hasBpm = EditorProjectSetupValidation.TryParseBpm(BaseBpmText, out var bpm);
        var fumenTarget = FumenMode == SetupFumenMode.Existing
            ? ExistingFumenTargetFileName
            : null;
        var fumenRequiresImport = FumenMode == SetupFumenMode.Existing &&
            RequiresImport(session.ExistingFumenFile!, fumenTarget!);
        var audioRequiresImport = RequiresImport(session.AudioFile!, TargetAudioFileName);
        var awbRequiresImport = session.AudioAwbFile is not null &&
            RequiresImport(session.AudioAwbFile, TargetAudioAwbFileName);

        selection = session.TakeSelection(
            ProjectName,
            ProjectFileNamePreview,
            FumenMode,
            FumenMode == SetupFumenMode.CreateNew ? FumenFileNamePreview : null,
            FumenMode == SetupFumenMode.CreateNew && hasBpm ? bpm : null,
            fumenTarget,
            TargetAudioFileName,
            RequiresExternalAwb ? TargetAudioAwbFileName : null,
            audioDuration,
            audioPackageKind,
            fumenRequiresImport,
            audioRequiresImport,
            awbRequiresImport);
        error = string.Empty;
        return true;
    }

    private void Revalidate()
    {
        if (RunState != EditorProjectSetupRunState.Editing)
        {
            OnPropertyChanged(nameof(CanCreate));
            return;
        }

        ValidationMessage = GetValidationError() ?? string.Empty;
    }

    private string? GetValidationError()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
            return "Enter a project name.";
        var projectStemResult = PortableEntryNameValidator.Validate(ProjectName);
        if (!projectStemResult.IsValid)
            return $"The project name is invalid ({projectStemResult.Error}).";
        if (ProjectName.EndsWith(
                FumenVisualEditorProviderBase.FILE_EXTENSION_NAME,
                StringComparison.OrdinalIgnoreCase))
            return $"Enter the project name without '{FumenVisualEditorProviderBase.FILE_EXTENSION_NAME}'.";
        var projectNameResult = PortableEntryNameValidator.Validate(ProjectFileNamePreview);
        if (!projectNameResult.IsValid)
            return $"The project file name is invalid ({projectNameResult.Error}).";
        if (EditorProjectSetupValidation.HasRootConflict(session.ProjectDirectory, ProjectFileNamePreview))
            return $"'{ProjectFileNamePreview}' already exists in the project folder.";
        if (session.AudioFile is null)
            return "Select an audio file.";
        if (PortableEntryNameValidator.Validate(TargetAudioFileName) is { IsValid: false } audioName)
            return $"The audio target name is invalid ({audioName.Error}).";
        if (!Path.GetExtension(TargetAudioFileName).Equals(
                Path.GetExtension(session.AudioFile.FileName),
                StringComparison.OrdinalIgnoreCase))
            return "The audio target must keep the selected audio file extension.";
        if (RequiresImport(session.AudioFile, TargetAudioFileName) &&
            EditorProjectSetupValidation.HasRootConflict(session.ProjectDirectory, TargetAudioFileName))
            return $"'{TargetAudioFileName}' already exists in the project folder.";

        if (RequiresExternalAwb)
        {
            if (session.AudioAwbFile is null)
                return $"Select the external AWB '{TargetAudioAwbFileName}'.";
            var awbName = PortableEntryNameValidator.Validate(TargetAudioAwbFileName);
            if (!awbName.IsValid)
                return $"The AWB target name is invalid ({awbName.Error}).";
            if (RequiresImport(session.AudioAwbFile, TargetAudioAwbFileName) &&
                EditorProjectSetupValidation.HasRootConflict(session.ProjectDirectory, TargetAudioAwbFileName))
                return $"'{TargetAudioAwbFileName}' already exists in the project folder.";
        }

        string fumenTarget;
        if (FumenMode == SetupFumenMode.Existing)
        {
            if (session.ExistingFumenFile is null)
                return "Select an existing fumen.";
            fumenTarget = ExistingFumenTargetFileName;
            var fumenName = PortableEntryNameValidator.Validate(fumenTarget);
            if (!fumenName.IsValid)
                return $"The fumen target name is invalid ({fumenName.Error}).";
            if (!Path.GetExtension(fumenTarget).Equals(
                    Path.GetExtension(session.ExistingFumenFile.FileName),
                    StringComparison.OrdinalIgnoreCase))
                return "The fumen target must keep the selected fumen file extension.";
            if (parserManager.GetSerializer(fumenTarget) is null ||
                parserManager.GetDeserializer(fumenTarget) is null)
                return "The selected fumen target format must be readable and writable.";
            if (RequiresImport(session.ExistingFumenFile, fumenTarget) &&
                EditorProjectSetupValidation.HasRootConflict(session.ProjectDirectory, fumenTarget))
                return $"'{fumenTarget}' already exists in the project folder.";
        }
        else
        {
            if (SelectedFumenFormat is null)
                return "Select a new fumen format.";
            var fumenStemResult = PortableEntryNameValidator.Validate(NewFumenStem);
            if (!fumenStemResult.IsValid)
                return $"The fumen name is invalid ({fumenStemResult.Error}).";
            var managedExtension = FumenFormatOptions
                .Select(option => option.Extension)
                .FirstOrDefault(extension => NewFumenStem.EndsWith(
                    extension,
                    StringComparison.OrdinalIgnoreCase));
            if (managedExtension is not null)
                return $"Enter the fumen name without '{managedExtension}'; choose the format separately.";
            fumenTarget = FumenFileNamePreview;
            var fumenName = PortableEntryNameValidator.Validate(fumenTarget);
            if (!fumenName.IsValid)
                return $"The fumen file name is invalid ({fumenName.Error}).";
            if (!EditorProjectSetupValidation.TryParseBpm(BaseBpmText, out _))
                return "Enter an initial BPM greater than zero.";
            if (EditorProjectSetupValidation.HasRootConflict(session.ProjectDirectory, fumenTarget))
                return $"'{fumenTarget}' already exists in the project folder.";
        }

        var generatedTargets = new[]
            {
                ProjectFileNamePreview,
                RequiresImport(session.AudioFile, TargetAudioFileName) ? TargetAudioFileName : null,
                RequiresExternalAwb && session.AudioAwbFile is not null &&
                    RequiresImport(session.AudioAwbFile, TargetAudioAwbFileName)
                    ? TargetAudioAwbFileName
                    : null,
                FumenMode == SetupFumenMode.CreateNew ||
                (session.ExistingFumenFile is not null && RequiresImport(session.ExistingFumenFile, fumenTarget))
                    ? fumenTarget
                    : null
            }
            .Where(name => name is not null)
            .Cast<string>();
        var duplicate = generatedTargets
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        return duplicate is null
            ? null
            : $"Multiple project files would use the name '{duplicate.Key}'.";
    }

    private bool RequiresImport(ISimpleFile file, string targetName) =>
        !EditorProjectSetupValidation.IsFileOwnedByDirectory(file, session.ProjectDirectory) ||
        !file.FileName.Equals(targetName, StringComparison.Ordinal);

    private ISimpleFile ResolveProjectOwnedCapability(ISimpleFile selected)
    {
        var match = EnumerateFiles(session.ProjectDirectory)
            .Concat(session.SourceDirectories.SelectMany(EnumerateFiles))
            .FirstOrDefault(candidate => IsSamePhysicalFile(candidate, selected));
        if (match is null)
            return selected;
        selected.Dispose();
        return match;
    }

    private void DisposeIfStandalone(ISimpleFile? file)
    {
        if (file is null)
            return;
        if (EditorProjectSetupValidation.IsFileOwnedByDirectory(file, session.ProjectDirectory) ||
            session.SourceDirectories.Any(root =>
                EditorProjectSetupValidation.IsFileOwnedByDirectory(file, root)))
            return;
        file.Dispose();
    }

    private static IEnumerable<ISimpleFile> EnumerateFiles(ISimpleDirectory root)
    {
        foreach (var file in root.ChildFiles)
            yield return file;
        foreach (var directory in root.ChildDictionaries)
            foreach (var file in EnumerateFiles(directory))
                yield return file;
    }

    private static bool IsSamePhysicalFile(ISimpleFile left, ISimpleFile right)
    {
        if (left.FullPath is not { } leftPath || right.FullPath is not { } rightPath)
            return false;
        try
        {
            return Path.GetFullPath(leftPath).Equals(
                Path.GetFullPath(rightPath),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static ISimpleFile? FindSibling(ISimpleFile file, string expectedName)
    {
        var matches = file.ParentDictionary?.ChildFiles
            .Where(candidate => candidate.FileName.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            .ToArray() ?? [];
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidDataException(
                $"Multiple sibling files are named '{expectedName}'.")
        };
    }

    private void NotifyAudioChanged()
    {
        OnPropertyChanged(nameof(AudioFileDisplayName));
        OnPropertyChanged(nameof(TargetAudioFileName));
        OnPropertyChanged(nameof(AudioAwbDisplayName));
        OnPropertyChanged(nameof(TargetAudioAwbFileName));
        OnPropertyChanged(nameof(RequiresExternalAwb));
        OnPropertyChanged(nameof(AudioDurationDisplay));
    }

    private bool TryBeginFinalizing()
    {
        lock (stateSync)
        {
            if (RunState != EditorProjectSetupRunState.Running)
                return false;
            RunState = EditorProjectSetupRunState.Finalizing;
            return true;
        }
    }

    private void BeginRollback()
    {
        lock (stateSync)
            RunState = EditorProjectSetupRunState.RollingBack;
    }

    private void RequestCancellation()
    {
        lock (stateSync)
        {
            if (RunState != EditorProjectSetupRunState.Running)
                return;
            RunState = EditorProjectSetupRunState.CancellationRequested;
            creationCancellation?.Cancel();
        }
    }

    private void UpdateProgress(EditorProjectCreationProgress progress)
    {
        if (RunState == EditorProjectSetupRunState.Completed)
            return;
        ProgressText = GetProgressText(progress.Phase);
        ProgressFileName = progress.CurrentFileName ?? string.Empty;
        IsProgressIndeterminate = progress.TotalBytes is null;
        ProgressMaximum = Math.Max(1, progress.TotalBytes ?? 1);
        ProgressValue = Math.Clamp(progress.BytesCompleted, 0, (long)ProgressMaximum);
    }

    private static string GetProgressText(EditorProjectCreationPhase phase) => phase switch
    {
        EditorProjectCreationPhase.WaitingForIoGate => "Waiting for other project operations...",
        EditorProjectCreationPhase.RefreshingDirectory => "Checking the project folder...",
        EditorProjectCreationPhase.ValidatingSelection => "Validating project settings...",
        EditorProjectCreationPhase.ParsingSourceFumen => "Reading the fumen...",
        EditorProjectCreationPhase.InspectingAudioPackage => "Inspecting the audio package...",
        EditorProjectCreationPhase.DecodingSourceAudio => "Validating the audio...",
        EditorProjectCreationPhase.PreparingNewFumen => "Generating the initial fumen...",
        EditorProjectCreationPhase.PreparingProjectData => "Generating project data...",
        EditorProjectCreationPhase.CopyingExternalAwb => "Copying the external AWB...",
        EditorProjectCreationPhase.CopyingAudio => "Copying the audio...",
        EditorProjectCreationPhase.CopyingFumen => "Copying the fumen...",
        EditorProjectCreationPhase.WritingNewFumen => "Writing the new fumen...",
        EditorProjectCreationPhase.WritingProjectFile => "Writing the project file...",
        EditorProjectCreationPhase.VerifyingCreatedFiles => "Checking the created files...",
        EditorProjectCreationPhase.LoadingCandidateContext => "Loading the new project...",
        EditorProjectCreationPhase.AttachingEditor => "Opening the new project...",
        EditorProjectCreationPhase.RollingBack => "Cleaning up incomplete project files...",
        _ => string.Empty
    };

    private static string BuildRollbackFailureMessage(IReadOnlyList<string> failures) =>
        "Some newly created files could not be removed:" + Environment.NewLine +
        string.Join(Environment.NewLine, failures.Select(failure => "- " + failure));

    private static string SuggestProjectName(string directoryName)
    {
        return string.IsNullOrEmpty(directoryName)
            ? "project"
            : directoryName;
    }

    private static string GetDisplayPath(ISimpleFile? file)
    {
        if (file is null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(file.FullPath))
            return file.FullPath;
        return file.FileName;
    }
}
