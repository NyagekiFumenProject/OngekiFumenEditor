using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.DragDrops;
using Gekimini.Avalonia.Framework.DragDrops.Behaviors;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenBulletPalleteListViewer.ViewModels;

[RegisterSingleton<IFumenBulletPalleteListViewer>]
public partial class FumenBulletPalleteListViewerViewModel : ToolViewModelBase, IFumenBulletPalleteListViewer
{
    private BulletPalleteList observedPalleteList;
    private bool draggingItem;
    private Point mouseStartPosition;
    private PointerPressedEventArgs pointerPressedEvent;
    private BulletPallete selectingPallete;

    public string Filter
    {
        get => field;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(field, value, OnEditorPropertyChanged);
            if (!SetProperty(ref field, value))
                return;

            BindPalleteList(value?.EditorContext?.Fumen?.BulletPalleteList);
            OnPropertyChanged(nameof(IsEnable));
            CreateNewCommand.NotifyCanExecuteChanged();
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsEnable => Editor?.EditorContext?.Fumen is not null;
    public ObservableCollection<BulletPallete> SelectedItems { get; } = [];
    public ObservableCollection<BulletPallete> DataView { get; } = [];

    public FumenBulletPalleteListViewerViewModel() : base(Lang.B.FumenBulletPalleteListViewer.ToLocalizedString())
    {
        Dock = global::Dock.Model.Core.DockMode.Bottom;
        SelectedItems.CollectionChanged += (_, _) => DeleteSelectedCommand.NotifyCanExecuteChanged();
        IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
        Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        Editor = @new;
    }

    private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FumenVisualEditorViewModel.EditorContext))
            BindPalleteList(Editor?.EditorContext?.Fumen?.BulletPalleteList);
    }

    private void BindPalleteList(BulletPalleteList palleteList)
    {
        if (observedPalleteList is not null)
            observedPalleteList.CollectionChanged -= OnPalleteListChanged;

        observedPalleteList = palleteList;
        if (observedPalleteList is not null)
            observedPalleteList.CollectionChanged += OnPalleteListChanged;

        RefreshFilter();
        OnPropertyChanged(nameof(IsEnable));
        CreateNewCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }

    private void OnPalleteListChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshFilter();
    }

    private bool CanModifyFumen() => IsEnable;

    [RelayCommand(CanExecute = nameof(CanModifyFumen))]
    private void CreateNew()
    {
        Log.LogInfo("CreateNew triggered.");
        Editor.EditorContext.Fumen.AddObject(new BulletPallete());
    }

    private bool CanDeleteSelected() => IsEnable && SelectedItems.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        Log.LogInfo("DeleteSelected triggered.");

        var affectedReferences = Editor.EditorContext.Fumen.Bells
            .OfType<IBulletPalleteReferencable>()
            .Concat(Editor.EditorContext.Fumen.Bullets)
            .Where(x => x.ReferenceBulletPallete is not null)
            .Where(x => SelectedItems.Contains(x.ReferenceBulletPallete));

        if (affectedReferences.FirstOrDefault() is { } firstObject)
        {
            await IoC.Get<IDialogManager>().ShowMessageDialog(
                Lang.CantDeleteReferencedBulletPallete.Format(firstObject.ReferenceBulletPallete.StrID));
            return;
        }

        foreach (var item in SelectedItems.ToArray())
            Editor.EditorContext.Fumen.RemoveObject(item);
    }

    public void OnCreateBulletPointerMoved(PointerEventArgs pointerEventArgs)
    {
        if (!TryBeginDrag(pointerEventArgs, out var triggerEvent))
            return;

        var dropParam = new OngekiObjectDropParam(() => new Bullet
        {
            ReferenceBulletPallete = selectingPallete
        });
        _ = IoC.Get<IDragDropManager>().StartDragDropEvent(triggerEvent, dropParam, DragDropEffects.Move);
        draggingItem = false;
    }

    public void OnCreateBellPointerMoved(PointerEventArgs pointerEventArgs)
    {
        if (!TryBeginDrag(pointerEventArgs, out var triggerEvent))
            return;

        var dropParam = new OngekiObjectDropParam(() => new Bell
        {
            ReferenceBulletPallete = selectingPallete
        });
        _ = IoC.Get<IDragDropManager>().StartDragDropEvent(triggerEvent, dropParam, DragDropEffects.Move);
        draggingItem = false;
    }

    private bool TryBeginDrag(PointerEventArgs pointerEventArgs, out PointerPressedEventArgs triggerEvent)
    {
        triggerEvent = null;
        if (!draggingItem || pointerPressedEvent is null)
            return false;

        if (!pointerEventArgs.Properties.IsLeftButtonPressed)
        {
            draggingItem = false;
            pointerPressedEvent = null;
            return false;
        }

        var diff = mouseStartPosition - pointerEventArgs.GetPosition(null);
        if (Math.Abs(diff.X) <= DragDataContextOutBehavior.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= DragDataContextOutBehavior.MinimumVerticalDragDistance)
        {
            return false;
        }

        triggerEvent = pointerPressedEvent;
        pointerPressedEvent = null;
        return true;
    }

    public void OnCreateObjectPointerPressed(Control source, PointerPressedEventArgs pointerEventArgs)
    {
        draggingItem = false;
        pointerPressedEvent = null;
        if (!pointerEventArgs.Properties.IsLeftButtonPressed ||
            source.DataContext is not BulletPallete pallete)
            return;

        mouseStartPosition = pointerEventArgs.GetPosition(null);
        selectingPallete = pallete;
        pointerPressedEvent = pointerEventArgs;
        draggingItem = true;
    }

    [RelayCommand]
    private void ClonePalette(BulletPallete pallete)
    {
        Log.LogInfo("ClonePalette triggered.");
        if (pallete is null || Editor?.EditorContext?.Fumen is null)
            return;

        var copiedPallete = new BulletPallete();
        copiedPallete.Copy(pallete);
        copiedPallete.StrID = null;
        Editor.EditorContext.Fumen.AddObject(copiedPallete);
    }

    [RelayCommand]
    private void SelectReferences(BulletPallete pallete)
    {
        Log.LogInfo("SelectReferences triggered.");
        if (pallete is null || Editor is null)
            return;

        Editor.TryCancelAllObjectSelecting();
        foreach (var selectable in Editor.EditorContext.Fumen.Bells
                     .OfType<IBulletPalleteReferencable>()
                     .Concat(Editor.EditorContext.Fumen.Bullets)
                     .Where(x => x.ReferenceBulletPallete == pallete)
                     .OfType<ISelectableObject>())
        {
            selectable.IsSelected = true;
        }

        IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
    }

    [RelayCommand]
    private void RefreshFilter()
    {
        Log.LogInfo("RefreshFilter triggered.");
        if (Editor?.EditorContext?.Fumen is null)
            return;

        DataView.Clear();

        foreach (var pallete in Editor.EditorContext.Fumen.BulletPalleteList.Where(pallete =>
                     string.IsNullOrWhiteSpace(Filter) ||
                     pallete.ToString().Contains(Filter, StringComparison.InvariantCultureIgnoreCase)))
        {
            DataView.Add(pallete);
        }
    }
}

