using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Modules.Shell;

namespace OngekiFumenEditor.Avalonia.Browser.Utils;

/// <summary>
///     跟踪所有已打开持久化文档的脏状态，并把聚合结果推给 JS 侧；
///     JS 在脏时挂接 beforeunload，让浏览器在关闭标签页/刷新前弹出原生确认框。
/// </summary>
public sealed class BeforeUnloadDirtyDocumentGuard
{
    private readonly Action<bool> setDirtyState;
    private readonly List<IPersistedDocumentViewModel> trackedDocuments = new();

    public BeforeUnloadDirtyDocumentGuard(Action<bool> setDirtyState)
    {
        ArgumentNullException.ThrowIfNull(setDirtyState);
        this.setDirtyState = setDirtyState;
    }

    public void Attach(IShell shell)
    {
        shell.DockableOpened += OnDockableOpened;
        shell.DockableClosed += OnDockableClosed;
        foreach (var document in shell.Documents.OfType<IPersistedDocumentViewModel>())
            Track(document);
    }

    private void OnDockableOpened(object sender, IDockableViewModel dockable)
    {
        if (dockable is IPersistedDocumentViewModel persistedDocument)
            Track(persistedDocument);
    }

    private void OnDockableClosed(object sender, IDockableViewModel dockable)
    {
        if (dockable is IPersistedDocumentViewModel persistedDocument)
            Untrack(persistedDocument);
    }

    private void Track(IPersistedDocumentViewModel document)
    {
        if (trackedDocuments.Contains(document))
            return;

        trackedDocuments.Add(document);
        document.PropertyChanged += OnDocumentPropertyChanged;
        PushState();
    }

    private void Untrack(IPersistedDocumentViewModel document)
    {
        if (!trackedDocuments.Remove(document))
            return;

        document.PropertyChanged -= OnDocumentPropertyChanged;
        PushState();
    }

    private void OnDocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IPersistedDocumentViewModel.IsDirty))
            PushState();
    }

    private void PushState()
    {
        // 提醒属于尽力而为：JS 桥不可用时不能影响编辑主流程。
        try
        {
            setDirtyState(trackedDocuments.Any(x => x.IsDirty));
        }
        catch (Exception)
        {
            // ignored
        }
    }
}
