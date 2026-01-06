using System.ComponentModel;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Framework.DragDrops;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Modules.Toolbox.ViewModels;
using Gekimini.Avalonia.Platforms.Services.Window;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Gekimini.Avalonia.Views;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.InternalTest.Models;
using OngekiFumenEditor.Avalonia.Modules.InternalTest.ToolboxItems;
using OngekiFumenEditor.Avalonia.Modules.InternalTest.ViewModels.Tools;
using OngekiFumenEditor.Avalonia.Modules.InternalTest.ViewModels.Windows;

namespace OngekiFumenEditor.Avalonia.Modules.InternalTest.ViewModels.Documents;

public partial class InternalTestDocumentViewModel : DocumentViewModelBase, IPersistedDocumentViewModel
{
    private IStorageFile storageFile;

    [GetServiceLazy]
    private partial ILogger<InternalTestDocumentViewModel> Logger { get; }

    [GetServiceLazy]
    private partial IDragDropManager DragDropManager { get; }

    [GetServiceLazy]
    private partial IWindowManager WindowManager { get; }

    [GetServiceLazy]
    private partial IServiceProvider ServiceProvider { get; }

    [GetServiceLazy]
    private partial IEditorRecentFilesManager EditorRecentFilesManager { get; }

    [GetServiceLazy]
    private partial IShell Shell { get; }

    [ObservableProperty]
    public partial int Value { get; set; }

    [ObservableProperty]
    public partial string FileName { get; set; }

    [GetServiceLazy]
    private partial IDialogManager DialogManager { get; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    public override void OnViewAfterLoaded(IView view)
    {
        base.OnViewAfterLoaded(view);

        if (view is Control control)
        {
            DragDrop.SetAllowDrop(control, true);
            DragDrop.AddDropHandler(control, OnDragDrop);
        }
    }

    public override void OnViewBeforeUnload(IView view)
    {
        base.OnViewBeforeUnload(view);

        if (view is Control control)
        {
            DragDrop.RemoveDropHandler(control, OnDragDrop);
            DragDrop.SetAllowDrop(control, false);
        }
    }

    [ObservableProperty]
    public partial bool IsNew { get; set; }

    public Task<bool> New()
    {
        FileName = "new file";
        IsNew = true;
        IsDirty = true;

        Value = 0;

        return Task.FromResult(true);
    }

    public async Task<bool> Load()
    {
        storageFile = (await (App.Current as App).TopLevel.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = Lang.InternalTestDocumentFileDialogTitle,
                FileTypeFilter = InternalDocumentEditorProvider.SupportFileTypes.BuildFileTypeFilters(),
                AllowMultiple = false
            })).FirstOrDefault();

        if (storageFile is null)
            return false;

        await using var fs = await storageFile.OpenReadAsync();
        var recentData = await JsonSerializer.DeserializeAsync(fs, InternalTestValueStoreData.JsonTypeInfo);

        return await DoLoad(recentData);
    }

    public async Task<bool> Load(RecentRecordInfo info)
    {
        //load from recent 
        var bookmark = EditorRecentFilesManager.ReadDataAsString(info);
        storageFile = await (App.Current as App).TopLevel.StorageProvider.OpenFileBookmarkAsync(bookmark);

        if (storageFile is null)
        {
            await DialogManager.ShowMessageDialog(
                Lang.InternalTestDocumentLoadFailedByBadRecentData, DialogMessageType.Error);
            return false;
        }

        await using var fs = await storageFile.OpenReadAsync();
        var recentData = await JsonSerializer.DeserializeAsync(fs, InternalTestValueStoreData.JsonTypeInfo);

        return await DoLoad(recentData);
    }

    public async Task<bool> SaveAs()
    {
        var newStorageFile = await (App.Current as App).TopLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                FileTypeChoices = InternalDocumentEditorProvider.SupportFileTypes.BuildFileTypeFilters()
            });

        if (newStorageFile is null)
        {
            Logger.LogInformationEx("newStorageFile is empty, skipped.");
            return false;
        }

        //overwrite current storage file.
        storageFile = newStorageFile;
        return await Save();
    }

    public async Task<bool> Save()
    {
        storageFile ??= await (App.Current as App).TopLevel.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                FileTypeChoices = InternalDocumentEditorProvider.SupportFileTypes.BuildFileTypeFilters()
            });

        if (storageFile is null)
        {
            await DialogManager.ShowMessageDialog(Lang.InternalTestDocumentCantSaveFile, DialogMessageType.Error);
            return false;
        }

        await using var fs = await storageFile.OpenWriteAsync();
        await JsonSerializer.SerializeAsync(fs, new InternalTestValueStoreData
            {
                StoredValue = Value,
                DocumentName = storageFile.Name
            },
            InternalTestValueStoreData.JsonTypeInfo);

        //await DialogManager.ShowMessageDialog("Saved document file successfully!");

        //if storageFile can get a bookmark that's mean we could post a recent to reuse storageFile(and its permissions) in feature.
        if (storageFile.CanBookmark)
        {
            var bookmark = await storageFile.SaveBookmarkAsync();
            //save recent
            var recentInfo = EditorRecentFilesManager.PostRecent(
                InternalDocumentEditorProvider.SupportFileTypes[0], FileName, bookmark);

            EditorRecentFilesManager.WriteDataAsString(recentInfo, bookmark);
        }

        FileName = storageFile.Name;
        IsNew = false;
        IsDirty = false;

        return true;
    }

    private async Task<bool> DoLoad(InternalTestValueStoreData recentData)
    {
        Value = recentData.StoredValue;
        IsNew = false;
        IsDirty = false;
        FileName = recentData.DocumentName;

        //if storageFile can get a bookmark that's mean we could post a recent to reuse storageFile(and its permissions) in feature.
        if (storageFile is not null && storageFile.CanBookmark)
        {
            var bookmark = await storageFile.SaveBookmarkAsync();
            //save recent
            var recentInfo = EditorRecentFilesManager.PostRecent(
                InternalDocumentEditorProvider.SupportFileTypes[0], FileName, bookmark);

            EditorRecentFilesManager.WriteDataAsString(recentInfo, bookmark);
        }

        return true;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FileName):
            case nameof(IsNew):
            case nameof(IsDirty):
                UpdateTitle();
                break;
            case nameof(Value):
                IsDirty = true;
                break;
        }

        base.OnPropertyChanged(e);
    }

    private void UpdateTitle()
    {
        var title = FileName;
        if (IsNew)
            title = "[New] " + title;
        /*
        if (IsDirty)
            title = "* " + title;
        */

        Title = LocalizedString.CreateFromRawText(title);
    }

    [RelayCommand]
    private void Increment()
    {
        var beforeValue = Value;
        UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.IncrementValue.ToLocalizedString(),
            () => Value++,
            () => Value = beforeValue));
    }

    [RelayCommand]
    private void Decrement()
    {
        var beforeValue = Value;
        UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.DecrementValue.ToLocalizedString(),
            () => Value--,
            () => Value = beforeValue));
    }

    [RelayCommand]
    private void Undo()
    {
        UndoRedoManager.Undo(1);
    }

    [RelayCommand]
    private void Redo()
    {
        UndoRedoManager.Redo(1);
    }

    private void OnDragDrop(object sender, DragEventArgs e)
    {
        if (DragDropManager.TryGetDragData(e, out var data))
        {
            if (data is ToolboxItemViewModel toolboxItemViewModel)
            {
                Logger.LogDebugEx($"toolboxItemViewModel.Model: {toolboxItemViewModel.Model}");

                switch (toolboxItemViewModel.Model)
                {
                    case AddItem:
                        Increment();
                        break;
                    case SubItem:
                        Decrement();
                        break;
                    case MultiplyItem:
                        Multiply();
                        break;
                }
            }

            //finish dragdrop
            DragDropManager.EndDragDropEvent(e);
        }
    }

    private void Multiply()
    {
        var beforeValue = Value;
        UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.MultiplyValue.ToLocalizedString(),
            () => Value *= Value,
            () => Value = beforeValue));
    }

    [RelayCommand(CanExecute = nameof(CanShowNewWindow))]
    private void ShowNewWindow()
    {
        WindowManager.ShowWindowAsync(new InternalTestWindowViewModel());
    }

    private bool CanShowNewWindow()
    {
        return true;
    }

    [RelayCommand]
    private void AddTool(string dockEnum)
    {
        var tool = ServiceProvider.Resolve<InternalTestToolViewModel>();
        tool.Dock = Enum.Parse<DockMode>(dockEnum);
        Shell.ShowTool(tool);
    }
}