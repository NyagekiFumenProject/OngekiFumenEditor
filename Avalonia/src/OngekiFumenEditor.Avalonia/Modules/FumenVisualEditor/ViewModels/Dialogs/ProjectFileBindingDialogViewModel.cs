#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using System.Collections.ObjectModel;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;

public sealed class ProjectFileBindingOption
{
    internal ProjectFileBindingOption(string displayName, ISimpleFile file, bool ownsFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(file);
        DisplayName = displayName;
        File = file;
        OwnsFile = ownsFile;
    }

    public string DisplayName { get; }

    internal ISimpleFile File { get; }

    internal bool OwnsFile { get; set; }
}

public partial class ProjectFileBindingDialogViewModel : WindowViewModelBase, IDisposable
{
    private readonly Func<Task<ISimpleFile?>> selectFumenFile;
    private readonly Func<Task<ISimpleFile?>> selectAudioFile;
    private ProjectFileBindingOption? ownedFumenOption;
    private ProjectFileBindingOption? ownedAudioOption;
    private bool isDisposed;

    public ProjectFileBindingDialogViewModel(
        string projectLocator,
        IEnumerable<(string Locator, ISimpleFile File)> fumenCandidates,
        IEnumerable<(string Locator, ISimpleFile File)> audioCandidates)
        : this(
            projectLocator,
            fumenCandidates,
            audioCandidates,
            () => FileDialogHelper.OpenFileAsync(
                "Select fumen file",
                FileDialogHelper.GetSupportFumenOpenFileExtensionFilterList()),
            () => FileDialogHelper.OpenFileAsync(
                "Select audio file",
                FileDialogHelper.GetSupportAudioFileExtensionFilterList()))
    {
    }

    internal ProjectFileBindingDialogViewModel(
        string projectLocator,
        IEnumerable<(string Locator, ISimpleFile File)> fumenCandidates,
        IEnumerable<(string Locator, ISimpleFile File)> audioCandidates,
        Func<Task<ISimpleFile?>> selectFumenFile,
        Func<Task<ISimpleFile?>> selectAudioFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectLocator);
        ArgumentNullException.ThrowIfNull(fumenCandidates);
        ArgumentNullException.ThrowIfNull(audioCandidates);
        ArgumentNullException.ThrowIfNull(selectFumenFile);
        ArgumentNullException.ThrowIfNull(selectAudioFile);

        ProjectLocator = projectLocator;
        FumenOptions = new ObservableCollection<ProjectFileBindingOption>(
            fumenCandidates.Select(candidate =>
                new ProjectFileBindingOption(candidate.Locator, candidate.File, ownsFile: false)));
        AudioOptions = new ObservableCollection<ProjectFileBindingOption>(
            audioCandidates.Select(candidate =>
                new ProjectFileBindingOption(candidate.Locator, candidate.File, ownsFile: false)));
        this.selectFumenFile = selectFumenFile;
        this.selectAudioFile = selectAudioFile;
    }

    public string ProjectLocator { get; }

    public ObservableCollection<ProjectFileBindingOption> FumenOptions { get; }

    public ObservableCollection<ProjectFileBindingOption> AudioOptions { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial ProjectFileBindingOption? SelectedFumenOption { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    public partial ProjectFileBindingOption? SelectedAudioOption { get; set; }

    private bool CanConfirm() =>
        SelectedFumenOption is not null && SelectedAudioOption is not null;

    [RelayCommand]
    private Task BrowseFumenAsync() =>
        BrowseAsync(
            selectFumenFile,
            FumenOptions,
            option =>
            {
                ReplaceOwnedOption(ref ownedFumenOption, FumenOptions, option);
                SelectedFumenOption = option;
            },
            "Unable to select the fumen file");

    [RelayCommand]
    private Task BrowseAudioAsync() =>
        BrowseAsync(
            selectAudioFile,
            AudioOptions,
            option =>
            {
                ReplaceOwnedOption(ref ownedAudioOption, AudioOptions, option);
                SelectedAudioOption = option;
            },
            "Unable to select the audio file");

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private Task ConfirmAsync() => TryCloseAsync(true);

    [RelayCommand]
    private Task CancelAsync() => TryCloseAsync(false);

    internal (ISimpleFile FumenFile, ISimpleFile AudioFile) TakeSelection()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        var fumenOption = SelectedFumenOption ??
            throw new InvalidOperationException("A fumen file has not been selected.");
        var audioOption = SelectedAudioOption ??
            throw new InvalidOperationException("An audio file has not been selected.");

        ReleaseSelectedOption(ref ownedFumenOption, fumenOption);
        ReleaseSelectedOption(ref ownedAudioOption, audioOption);
        DisposeOwnedOption(ref ownedFumenOption, FumenOptions);
        DisposeOwnedOption(ref ownedAudioOption, AudioOptions);
        return (fumenOption.File, audioOption.File);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        DisposeOwnedOption(ref ownedFumenOption, FumenOptions);
        DisposeOwnedOption(ref ownedAudioOption, AudioOptions);
        GC.SuppressFinalize(this);
    }

    private static async Task BrowseAsync(
        Func<Task<ISimpleFile?>> selector,
        ObservableCollection<ProjectFileBindingOption> options,
        Action<ProjectFileBindingOption> applySelection,
        string failureMessage)
    {
        ISimpleFile? file = null;
        try
        {
            file = await selector();
            if (file is null)
                return;

            var option = new ProjectFileBindingOption(
                GetDisplayPath(file),
                file,
                ownsFile: true);
            options.Add(option);
            applySelection(option);
            file = null;
        }
        catch (Exception exception)
        {
            await IoC.Get<IDialogManager>().ShowMessageDialog(
                $"{failureMessage}: {exception.Message}",
                DialogMessageType.Error);
        }
        finally
        {
            file?.Dispose();
        }
    }

    private static string GetDisplayPath(ISimpleFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.LocalPath))
            return file.LocalPath;
        if (!string.IsNullOrWhiteSpace(file.FullPath))
            return file.FullPath;
        return file.FileName;
    }

    private static void ReplaceOwnedOption(
        ref ProjectFileBindingOption? current,
        ObservableCollection<ProjectFileBindingOption> options,
        ProjectFileBindingOption replacement)
    {
        DisposeOwnedOption(ref current, options);
        current = replacement;
    }

    private static void ReleaseSelectedOption(
        ref ProjectFileBindingOption? ownedOption,
        ProjectFileBindingOption selectedOption)
    {
        if (!ReferenceEquals(ownedOption, selectedOption))
            return;

        selectedOption.OwnsFile = false;
        ownedOption = null;
    }

    private static void DisposeOwnedOption(
        ref ProjectFileBindingOption? option,
        ObservableCollection<ProjectFileBindingOption> options)
    {
        var previous = option;
        option = null;
        if (previous is null)
            return;

        options.Remove(previous);
        if (previous.OwnsFile)
        {
            previous.OwnsFile = false;
            previous.File.Dispose();
        }
    }
}
