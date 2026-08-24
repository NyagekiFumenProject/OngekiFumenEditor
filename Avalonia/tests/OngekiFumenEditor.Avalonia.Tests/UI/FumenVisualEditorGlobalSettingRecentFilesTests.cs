using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Modules.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.FumenVisualEditor.Views;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class FumenVisualEditorGlobalSettingRecentFilesTests
{
    [AvaloniaFact]
    public async Task ClearRecentFilesCommand_WhenCancelled_PreservesRecords()
    {
        var dialogManager = new StubDialogManager(false);
        var recentFilesManager = new StubRecentFilesManager(recordCount: 1);
        var viewModel = new FumenVisualEditorGlobalSettingViewModel(dialogManager, recentFilesManager);

        await viewModel.ClearRecentFilesCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogManager.ConfirmCallCount);
        Assert.Equal(Lang.CleanRecentFilesRecordsConfirm, dialogManager.LastContent);
        Assert.Equal(Lang.Warning, dialogManager.LastTitle);
        Assert.Equal(0, recentFilesManager.ClearCallCount);
        Assert.Single(recentFilesManager.RecentRecordInfos);
    }

    [AvaloniaFact]
    public async Task ClearRecentFilesCommand_WhenConfirmed_ClearsRecordsOnce()
    {
        var dialogManager = new StubDialogManager(true);
        var recentFilesManager = new StubRecentFilesManager(recordCount: 2);
        var viewModel = new FumenVisualEditorGlobalSettingViewModel(dialogManager, recentFilesManager);

        await viewModel.ClearRecentFilesCommand.ExecuteAsync(null);

        Assert.Equal(1, recentFilesManager.ClearCallCount);
        Assert.Empty(recentFilesManager.RecentRecordInfos);
    }

    [AvaloniaFact]
    public async Task ClearRecentFilesCommand_EmptyHistory_IsIdempotent()
    {
        var recentFilesManager = new StubRecentFilesManager(recordCount: 0);
        var viewModel = new FumenVisualEditorGlobalSettingViewModel(new StubDialogManager(true),
            recentFilesManager);

        await viewModel.ClearRecentFilesCommand.ExecuteAsync(null);
        await viewModel.ClearRecentFilesCommand.ExecuteAsync(null);

        Assert.Equal(2, recentFilesManager.ClearCallCount);
        Assert.Empty(recentFilesManager.RecentRecordInfos);
    }

    [AvaloniaFact]
    public void View_ExposesLocalizedClearRecentFilesCommand()
    {
        var viewModel = new FumenVisualEditorGlobalSettingViewModel(new StubDialogManager(true),
            new StubRecentFilesManager(recordCount: 1));
        var view = new FumenVisualEditorGlobalSettingView { DataContext = viewModel };

        var button = Assert.IsType<Button>(view.FindControl<Button>("ClearRecentFilesButton"));

        Assert.Same(viewModel.ClearRecentFilesCommand, button.Command);
        Assert.Equal(Lang.CleanRecentFilesRecords, button.Content?.ToString());
    }

    private sealed class StubDialogManager(bool confirmResult) : IDialogManager
    {
        public int ConfirmCallCount { get; private set; }
        public string? LastContent { get; private set; }
        public string? LastTitle { get; private set; }

        public Task<T> ShowDialog<T>() where T : DialogViewModelBase =>
            Task.FromException<T>(new NotSupportedException());

        public Task ShowDialog(DialogViewModelBase dialogViewModel) =>
            Task.FromException(new NotSupportedException());

        public Task ShowMessageDialog(string content, DialogMessageType messageType = DialogMessageType.Info) =>
            Task.FromException(new NotSupportedException());

        public Task<bool> ShowComfirmDialog(
            string content,
            string? title = null,
            string? yesButtonContent = null,
            string? noButtonContent = null)
        {
            ConfirmCallCount++;
            LastContent = content;
            LastTitle = title;
            return Task.FromResult(confirmResult);
        }
    }

    private sealed class StubRecentFilesManager : IEditorRecentFilesManager
    {
        private readonly List<RecentRecordInfo> records;

        public StubRecentFilesManager(int recordCount)
        {
            records = Enumerable.Range(0, recordCount)
                .Select(i => new RecentRecordInfo(
                    "test",
                    $"Project {i}",
                    $"project-{i}",
                    RecordId: Guid.NewGuid()))
                .ToList();
        }

        public int ClearCallCount { get; private set; }

        public IEnumerable<RecentRecordInfo> RecentRecordInfos => records;

        public void ClearAllRecordsAndDatas()
        {
            ClearCallCount++;
            records.Clear();
        }

        public RecentRecordInfo PostRecent(
            EditorFileType editorFileType,
            string name,
            string locationDescription,
            byte[]? data = null) => throw new NotSupportedException();

        public RecentRecordInfo UpdateRecent(
            Guid recordId,
            string name,
            string locationDescription,
            byte[]? data = null) => throw new NotSupportedException();

        public bool RemoveRecent(Guid recordId) => throw new NotSupportedException();
        public byte[]? ReadData(RecentRecordInfo info) => throw new NotSupportedException();
        public void WriteData(RecentRecordInfo info, byte[] data) => throw new NotSupportedException();
        public void ClearData(RecentRecordInfo info) => throw new NotSupportedException();
        public bool IsMarkedInvalid(RecentRecordInfo info) => throw new NotSupportedException();
        public void SetMarkedInvalid(RecentRecordInfo info, bool isInvalid) => throw new NotSupportedException();
    }
}
