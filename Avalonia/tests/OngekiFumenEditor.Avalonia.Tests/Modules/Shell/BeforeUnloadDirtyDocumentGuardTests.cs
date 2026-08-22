using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Browser.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.Shell;

public sealed partial class BeforeUnloadDirtyDocumentGuardTests
{
    private static async Task<IShell> PrepareShellAsync()
    {
        var shell = IoC.Get<IShell>();
        await shell.ResetLayout();
        return shell;
    }

    [AvaloniaFact]
    public async Task AttachThenOpenCleanDocument_PushesNotDirty()
    {
        var shell = await PrepareShellAsync();
        var pushed = new List<bool>();
        new BeforeUnloadDirtyDocumentGuard(pushed.Add).Attach(shell);

        var document = new StubPersistedDocument();
        await shell.OpenDocumentAsync(document);

        Assert.Equal(false, pushed.Last());
    }

    [AvaloniaFact]
    public async Task DocumentBecomesDirty_PushesDirty()
    {
        var shell = await PrepareShellAsync();
        var pushed = new List<bool>();
        new BeforeUnloadDirtyDocumentGuard(pushed.Add).Attach(shell);

        var document = new StubPersistedDocument();
        await shell.OpenDocumentAsync(document);
        document.IsDirty = true;

        Assert.Equal(true, pushed.Last());
    }

    [AvaloniaFact]
    public async Task SecondCleanDocument_OpenedWhileFirstDirty_StaysDirty()
    {
        var shell = await PrepareShellAsync();
        var pushed = new List<bool>();
        new BeforeUnloadDirtyDocumentGuard(pushed.Add).Attach(shell);

        var first = new StubPersistedDocument { IsDirty = true };
        await shell.OpenDocumentAsync(first);
        var second = new AnotherStubPersistedDocument();
        await shell.OpenDocumentAsync(second);

        Assert.Equal(true, pushed.Last());
    }

    [AvaloniaFact]
    public async Task DirtyDocumentClosed_CleanDocumentRemains_PushesNotDirty()
    {
        var shell = await PrepareShellAsync();
        var pushed = new List<bool>();
        new BeforeUnloadDirtyDocumentGuard(pushed.Add).Attach(shell);

        var first = new StubPersistedDocument { IsDirty = true };
        await shell.OpenDocumentAsync(first);
        var second = new AnotherStubPersistedDocument();
        await shell.OpenDocumentAsync(second);

        // 放弃保存关闭脏文档后，仅剩干净文档，聚合结果应回到非脏。
        await shell.TryCloseDocumentAsync(first);

        Assert.Equal(false, pushed.Last());
        Assert.Contains(second, shell.Documents);
    }

    [AvaloniaFact]
    public async Task DocumentClearedDirty_PushesNotDirty()
    {
        var shell = await PrepareShellAsync();
        var pushed = new List<bool>();
        new BeforeUnloadDirtyDocumentGuard(pushed.Add).Attach(shell);

        var document = new StubPersistedDocument { IsDirty = true };
        await shell.OpenDocumentAsync(document);
        Assert.Equal(true, pushed.Last());

        document.IsDirty = false;

        Assert.Equal(false, pushed.Last());
    }

    private partial class StubPersistedDocument : DocumentViewModelBase, IPersistedDocumentViewModel
    {
        [ObservableProperty]
        private bool isDirty;

        [ObservableProperty]
        private bool isNew;

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
            IsDirty = false;
            return Task.FromResult(true);
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
