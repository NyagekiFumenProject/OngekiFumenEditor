using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Dock.Model.Controls;
using Dock.Model.Core;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Models.Events;
using Gekimini.Avalonia.Modules.Documents.Models;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Modules.Shell.Commands;
using Gekimini.Avalonia.Modules.Shell.ViewModels;
using OngekiFumenEditor.Avalonia;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.Shell;

public sealed partial class DocumentCloseSafetyTests
{
    public DocumentCloseSafetyTests()
    {
        ProgrammableDialogManager.Instance.Reset();
    }

    private static async Task<ShellViewModel> PrepareShellWithDocumentAsync(StubPersistedDocument document)
    {
        var shell = IoC.Get<IShell>();
        ((ShellViewModel)shell).IsActive = true;
        await shell.ResetLayout();
        // ResetLayout 会顺带确认清理上一个测试残留的脏文档，
        // 这里重置计数器，保证后续断言只统计本测试自己的对话框。
        ProgrammableDialogManager.Instance.Reset();
        await shell.OpenDocumentAsync(document);
        Assert.Contains(document, shell.Documents);
        return (ShellViewModel)shell;
    }

    private static IDocument GetDocumentContainer(ShellViewModel shell, StubPersistedDocument document)
    {
        // headless 测试没有 DockControls，Factory.Find 恒为空，这里直接遍历布局树。
        return EnumerateDockables(shell.Layout)
            .OfType<IDocument>()
            .Single(x => ReferenceEquals(x.Context, document));
    }

    private static IEnumerable<IDockable> EnumerateDockables(IDockable dockable)
    {
        yield return dockable;

        if (dockable is IDock dock && dock.VisibleDockables is not null)
            foreach (var child in dock.VisibleDockables)
            foreach (var nested in EnumerateDockables(child))
                yield return nested;
    }

    private static async Task<bool> AskApplicationQuitOnceAsync()
    {
        await using var itor = WeakReferenceMessenger.Default
            .Send<ApplicationAskQuitEvent>()
            .GetAsyncEnumerator();
        while (await itor.MoveNextAsync())
        {
            if (!itor.Current)
                return false;
        }

        return true;
    }

    [AvaloniaFact]
    public async Task MenuCloseHandler_CleanDocument_ClosesWithoutDialog()
    {
        var document = new StubPersistedDocument();
        var shell = await PrepareShellWithDocumentAsync(document);
        Assert.Same(document, shell.ActiveDocument);

        var handler = new CloseFileCommandHandler(
            ((global::Gekimini.Avalonia.App)global::Avalonia.Application.Current).ServiceProvider);
        await handler.Run(null!);

        Assert.DoesNotContain(document, shell.Documents);
        Assert.Equal(0, ProgrammableDialogManager.Instance.DirtyDialogCount);
    }

    [AvaloniaFact]
    public async Task CloseProtocol_DirtyDocument_SaveSucceeds_Closes()
    {
        var document = new StubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(document);
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.Yes;

        var result = await shell.TryCloseDocumentAsync(document);

        Assert.Equal(RequestDocumentCloseResult.Closed, result);
        Assert.Equal(1, document.SaveCallCount);
        Assert.DoesNotContain(document, shell.Documents);
        Assert.Null(shell.ActiveDocument);
        Assert.Equal(0, ProgrammableDialogManager.Instance.MessageDialogCount);

        // 关闭后同类型文档可以重新打开，说明容器映射已被清理。
        await shell.OpenDocumentAsync(document);
        Assert.Contains(document, shell.Documents);
    }

    [AvaloniaFact]
    public async Task CloseProtocol_DirtyDocument_SaveFails_KeepsDocumentOpen()
    {
        var document = new StubPersistedDocument { IsDirty = true, SaveResult = false };
        var shell = await PrepareShellWithDocumentAsync(document);
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.Yes;

        var result = await shell.TryCloseDocumentAsync(document);

        Assert.Equal(RequestDocumentCloseResult.SaveFailed, result);
        Assert.Equal(1, document.SaveCallCount);
        Assert.Contains(document, shell.Documents);
        Assert.True(document.IsDirty);
        Assert.Equal(1, ProgrammableDialogManager.Instance.MessageDialogCount);
    }

    [AvaloniaFact]
    public async Task CloseProtocol_DirtyDocument_Discard_ClosesWithoutSave()
    {
        var document = new StubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(document);
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.No;

        var result = await shell.TryCloseDocumentAsync(document);

        Assert.Equal(RequestDocumentCloseResult.Closed, result);
        Assert.Equal(0, document.SaveCallCount);
        Assert.DoesNotContain(document, shell.Documents);
    }

    [AvaloniaFact]
    public async Task CloseProtocol_DirtyDocument_Cancel_KeepsDocumentOpen()
    {
        var document = new StubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(document);
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.Cancel;

        var result = await shell.TryCloseDocumentAsync(document);

        Assert.Equal(RequestDocumentCloseResult.Cancelled, result);
        Assert.Equal(0, document.SaveCallCount);
        Assert.Contains(document, shell.Documents);
    }

    [AvaloniaFact]
    public async Task CloseProtocol_CleanDocument_ClosesWithoutDialog()
    {
        var document = new StubPersistedDocument();
        var shell = await PrepareShellWithDocumentAsync(document);

        var result = await shell.TryCloseDocumentAsync(document);

        Assert.Equal(RequestDocumentCloseResult.Closed, result);
        Assert.Equal(0, ProgrammableDialogManager.Instance.DirtyDialogCount);
        Assert.DoesNotContain(document, shell.Documents);
    }

    [AvaloniaFact]
    public async Task CloseProtocol_ClosedDocument_CloseAgainIsIdempotent()
    {
        var document = new StubPersistedDocument();
        var shell = await PrepareShellWithDocumentAsync(document);
        await shell.TryCloseDocumentAsync(document);

        var result = await shell.TryCloseDocumentAsync(document);

        Assert.Equal(RequestDocumentCloseResult.Closed, result);
        Assert.DoesNotContain(document, shell.Documents);
    }

    [AvaloniaFact]
    public async Task TabClose_DirtyDocument_Cancel_KeepsDocumentOpen()
    {
        var document = new StubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(document);
        var container = GetDocumentContainer(shell, document);
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.Cancel;

        shell.Factory.CloseDockable(container);

        // CloseDockable 是 async void，让确认流程跑完。
        await WaitCloseDispatchAsync();

        Assert.Equal(1, ProgrammableDialogManager.Instance.DirtyDialogCount);
        Assert.Contains(document, shell.Documents);
    }

    [AvaloniaFact]
    public async Task TabClose_DirtyDocument_Discard_RemovesDocument()
    {
        var document = new StubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(document);
        var container = GetDocumentContainer(shell, document);
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.No;

        shell.Factory.CloseDockable(container);
        await WaitCloseDispatchAsync();

        Assert.Equal(1, ProgrammableDialogManager.Instance.DirtyDialogCount);
        Assert.Equal(0, document.SaveCallCount);
        Assert.DoesNotContain(document, shell.Documents);
        Assert.DoesNotContain(container, EnumerateDockables(shell.Layout));
    }

    [AvaloniaFact]
    public async Task TabClose_CleanDocument_RemovesWithoutDialog()
    {
        var document = new StubPersistedDocument();
        var shell = await PrepareShellWithDocumentAsync(document);
        var container = GetDocumentContainer(shell, document);

        shell.Factory.CloseDockable(container);
        await WaitCloseDispatchAsync();

        Assert.Equal(0, ProgrammableDialogManager.Instance.DirtyDialogCount);
        Assert.DoesNotContain(document, shell.Documents);
    }

    [AvaloniaFact]
    public async Task ResetLayout_DirtyDocument_AsksBeforeDiscard()
    {
        var document = new StubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(document);
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.No;

        await shell.ResetLayout();

        Assert.Equal(1, ProgrammableDialogManager.Instance.DirtyDialogCount);
        Assert.Empty(shell.Documents);
    }

    [AvaloniaFact]
    public async Task ExitAsk_DirtyDocumentCancelled_BlocksQuitWithoutRemovingDocument()
    {
        var document = new StubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(document);
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.Cancel;

        var canExit = await AskApplicationQuitOnceAsync();

        Assert.False(canExit);
        Assert.Contains(document, shell.Documents);
    }

    [AvaloniaFact]
    public async Task ExitAsk_MultipleDirtyDocuments_SecondCancels_StopsAsking()
    {
        var first = new StubPersistedDocument { IsDirty = true };
        var second = new AnotherStubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(first);
        await shell.OpenDocumentAsync(second);
        // 第一个文档放弃保存，第二个取消，退出流程应中止且不再继续询问。
        ProgrammableDialogManager.Instance.ScriptDirtyDialogAnswers(DialogResult.No, DialogResult.Cancel);

        var canExit = await AskApplicationQuitOnceAsync();

        Assert.False(canExit);
        // 守卫流程只确认不删除，两个文档都必须保持打开。
        Assert.Contains(first, shell.Documents);
        Assert.Contains(second, shell.Documents);
        Assert.Equal(2, ProgrammableDialogManager.Instance.DirtyDialogCount);
    }

    [AvaloniaFact]
    public async Task ExitAttempt_WhileConfirmDialogOpen_SecondAttemptRejectedImmediately()
    {
        var document = new StubPersistedDocument { IsDirty = true };
        var shell = await PrepareShellWithDocumentAsync(document);
        var app = (global::Gekimini.Avalonia.App)global::Avalonia.Application.Current;
        var dialogGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ProgrammableDialogManager.Instance.DirtyDialogGate = dialogGate;
        ProgrammableDialogManager.Instance.DirtyDialogAnswer = DialogResult.Cancel;

        // 第一次退出卡在脏确认对话框上，期间再次退出应被立即拒绝，不得并发进入第二轮确认。
        var firstAttempt = app.TryExit();
        var secondAttempt = await app.TryExit();

        Assert.False(secondAttempt);
        Assert.False(firstAttempt.IsCompleted);

        dialogGate.SetResult();

        Assert.False(await firstAttempt);
        Assert.Equal(1, ProgrammableDialogManager.Instance.DirtyDialogCount);
        Assert.Contains(document, shell.Documents);

        // 用户取消后退出流程结束，再次退出允许重新进入确认。
        var thirdAttempt = await app.TryExit();
        Assert.False(thirdAttempt);
        Assert.Equal(2, ProgrammableDialogManager.Instance.DirtyDialogCount);
    }

    private static Task WaitCloseDispatchAsync()
    {
        return Task.Delay(50);
    }

    private partial class StubPersistedDocument : DocumentViewModelBase, IPersistedDocumentViewModel    {
        [ObservableProperty]
        private bool isDirty;

        [ObservableProperty]
        private bool isNew;

        public bool SaveResult { get; set; } = true;

        public int SaveCallCount { get; private set; }

        public Task<bool> New()
        {
            return Task.FromResult(true);
        }

        public Task<bool> Load()
        {
            return Task.FromResult(true);
        }

        public Task<bool> Load(RecentRecordInfo info)
        {
            return Task.FromResult(true);
        }

        public Task<bool> Save()
        {
            SaveCallCount++;
            return Task.FromResult(SaveResult);
        }

        public Task<bool> SaveAs()
        {
            return Task.FromResult(false);
        }
    }

    private sealed partial class AnotherStubPersistedDocument : StubPersistedDocument
    {
    }
}
