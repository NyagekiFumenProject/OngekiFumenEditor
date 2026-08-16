using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class ProjectFileBindingDialogViewModelTests
{
    [Fact]
    public void SingleCandidates_StillRequireExplicitSelection()
    {
        var fumenFile = new TrackingFile("chart.nyageki");
        var audioFile = new TrackingFile("audio.wav");
        using var viewModel = new ProjectFileBindingDialogViewModel(
            "project.nyagekiProj",
            [("chart.nyageki", fumenFile)],
            [("audio.wav", audioFile)]);

        Assert.Null(viewModel.SelectedFumenOption);
        Assert.Null(viewModel.SelectedAudioOption);
        Assert.False(viewModel.ConfirmCommand.CanExecute(null));

        viewModel.SelectedFumenOption = Assert.Single(viewModel.FumenOptions);
        viewModel.SelectedAudioOption = Assert.Single(viewModel.AudioOptions);

        Assert.True(viewModel.ConfirmCommand.CanExecute(null));
        var selection = viewModel.TakeSelection();
        Assert.Same(fumenFile, selection.FumenFile);
        Assert.Same(audioFile, selection.AudioFile);
        Assert.Equal(0, fumenFile.DisposeCount);
        Assert.Equal(0, audioFile.DisposeCount);
    }

    [Fact]
    public async Task Dispose_ReleasesBrowsedFilesThatWereNotTransferred()
    {
        var fumenFile = new TrackingFile("external.nyageki");
        var audioFile = new TrackingFile("external.wav");
        var viewModel = new ProjectFileBindingDialogViewModel(
            "project.nyagekiProj",
            [],
            [],
            () => Task.FromResult<ISimpleFile?>(fumenFile),
            () => Task.FromResult<ISimpleFile?>(audioFile));

        await viewModel.BrowseFumenCommand.ExecuteAsync(null);
        await viewModel.BrowseAudioCommand.ExecuteAsync(null);
        viewModel.Dispose();

        Assert.Equal(1, fumenFile.DisposeCount);
        Assert.Equal(1, audioFile.DisposeCount);
    }

    [Fact]
    public async Task TakeSelection_TransfersBrowsedFileOwnership()
    {
        var fumenFile = new TrackingFile("external.nyageki");
        var audioFile = new TrackingFile("external.wav");
        using var viewModel = new ProjectFileBindingDialogViewModel(
            "project.nyagekiProj",
            [],
            [],
            () => Task.FromResult<ISimpleFile?>(fumenFile),
            () => Task.FromResult<ISimpleFile?>(audioFile));

        await viewModel.BrowseFumenCommand.ExecuteAsync(null);
        await viewModel.BrowseAudioCommand.ExecuteAsync(null);
        var selection = viewModel.TakeSelection();

        Assert.Same(fumenFile, selection.FumenFile);
        Assert.Same(audioFile, selection.AudioFile);
        Assert.Equal(0, fumenFile.DisposeCount);
        Assert.Equal(0, audioFile.DisposeCount);
    }

    private sealed class TrackingFile(string fileName) : ISimpleFile
    {
        private bool isDisposed;

        public int DisposeCount { get; private set; }
        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => $"memory:///{FileName}";
        public string? LocalPath => null;
        public string FileName { get; } = fileName;
        public long FileLength => 0;

        public ValueTask<string[]> ReadAllLines() => throw new NotSupportedException();

        public ValueTask<byte[]> ReadAllBytes() => throw new NotSupportedException();

        public Task<Stream> OpenRead() => throw new NotSupportedException();

        public Task<Stream> OpenWrite() => throw new NotSupportedException();

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            DisposeCount++;
        }
    }
}
