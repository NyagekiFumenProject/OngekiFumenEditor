using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Views;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Views;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.ViewModels
{
    [Export(typeof(IFumenBulletPalleteListViewer))]
    public class FumenBulletPalleteListViewerViewModel : Tool, IFumenBulletPalleteListViewer
    {
        public FumenBulletPalleteListViewerViewModel()
        {
            ToggleBulletPlacementCommand = new RelayCommand(parameter => ToggleProjectilePlacement(parameter, false), CanToggleProjectilePlacement);
            ToggleBellPlacementCommand = new RelayCommand(parameter => ToggleProjectilePlacement(parameter, true), CanToggleProjectilePlacement);
            DisplayName = Resources.FumenBulletPalleteListViewer;
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, Editor))
                return;

            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
            {
                DataView = Editor?.Fumen is { } fumen ? CollectionViewSource.GetDefaultView(fumen.BulletPalleteList) : null;
                NotifyOfPropertyChange(() => IsEnable);
            }

            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(FumenVisualEditorViewModel.RequestBatchPlaceProjectileInfo) ||
                e.PropertyName == nameof(FumenVisualEditorViewModel.IsRequestPlaceBatchProjectilePreview))
                NotifyPlacementRequestChanged();

            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(FumenVisualEditorViewModel.CanRequestBatchProjectilePlacement) ||
                e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
                CommandManager.InvalidateRequerySuggested();
        }

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        private string filter;
        public string Filter
        {
            get => filter;
            set => Set(ref filter, value);
        }

        private FumenVisualEditorViewModel editor;
        public FumenVisualEditorViewModel Editor
        {
            get
            {
                return editor;
            }
            set
            {
                if (ReferenceEquals(editor, value))
                    return;

                if (editor is not null)
                {
                    editor.PropertyChanged -= OnEditorPropertyChanged;
                    editor.CancelBatchProjectilePlacement();
                }

                Set(ref editor, value);
                if (editor is not null)
                    editor.PropertyChanged += OnEditorPropertyChanged;

                NotifyPlacementRequestChanged();
                NotifyOfPropertyChange(() => IsEnable);

                if (Editor?.Fumen is not null)
                    DataView = CollectionViewSource.GetDefaultView(Editor.Fumen.BulletPalleteList);
                else
                    DataView = null;

                DataView?.Refresh();
            }
        }

        public bool IsEnable => Editor?.Fumen is not null;

        public IPlaceBatchProjectileInfo RequestBatchPlaceProjectileInfo => Editor?.RequestBatchPlaceProjectileInfo;
        public bool IsRequestPlaceBatchProjectilePreview => RequestBatchPlaceProjectileInfo is not null;

        public ICommand ToggleBulletPlacementCommand { get; }
        public ICommand ToggleBellPlacementCommand { get; }

        private bool CanToggleProjectilePlacement(object parameter)
        {
            if (parameter is not BulletPallete pallete || Editor?.CanRequestBatchProjectilePlacement != true || Editor.Fumen is not { } fumen)
                return false;

            foreach (var existing in fumen.BulletPalleteList)
            {
                if (ReferenceEquals(existing, pallete))
                    return true;
            }
            return false;
        }

        private void ToggleProjectilePlacement(object parameter, bool isBell)
        {
            if (CanToggleProjectilePlacement(parameter))
                Editor.ToggleBatchProjectilePlacement((BulletPallete)parameter, isBell);

            NotifyPlacementRequestChanged();
        }

        private void NotifyPlacementRequestChanged()
        {
            NotifyOfPropertyChange(() => RequestBatchPlaceProjectileInfo);
            NotifyOfPropertyChange(() => IsRequestPlaceBatchProjectilePreview);
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnDataViewCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public ObservableCollection<BulletPallete> SelectedItems { get; } = new();

        private ICollectionView dataView;
        public ICollectionView DataView
        {
            get => dataView;
            set
            {
                if (dataView is not null)
                    dataView.CollectionChanged -= OnDataViewCollectionChanged;
                Set(ref dataView, value);
                if (dataView is not null)
                    dataView.CollectionChanged += OnDataViewCollectionChanged;
                CommandManager.InvalidateRequerySuggested();
                OnRefreshFilter();
            }
        }

        public void OnCreateNew()
        {
            var plattele = new BulletPallete();
            Editor.Fumen.AddObject(plattele);
            DataView?.Refresh();
        }

        public void OnDeleteSelecting(FumenBulletPalleteListViewerView e)
        {
            var affactedReferences = Editor.Fumen.Bells
                .OfType<IBulletPalleteReferencable>()
                .Concat(Editor.Fumen.Bullets)
                .Where(x => x.ReferenceBulletPallete != null)
                .Where(x => SelectedItems.Contains(x.ReferenceBulletPallete));

            if (affactedReferences.FirstOrDefault() is { } firstObj)
            {
                MessageBox.Show(Resources.CantDeleteReferencedBulletPallete.Format(firstObj.ReferenceBulletPallete.StrID));
                return;
            }

            foreach (var item in SelectedItems.ToArray())
                Editor.Fumen.RemoveObject(item);
            DataView?.Refresh();
        }

        private bool _draggingItem;
        private Point _mouseStartPosition;
        private BulletPallete _selecting;

        public void OnMouseMoveAndDragNewBullet(ActionExecutionContext e)
        {
            if (!_draggingItem)
                return;
            var arg = e.EventArgs as MouseEventArgs;

            Point mousePosition = arg.GetPosition(null);
            Vector diff = _mouseStartPosition - mousePosition;

            if (arg.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                Editor?.CancelBatchProjectilePlacement();
                var dragData = new DataObject(ToolboxDragDrop.DataFormat, new OngekiObjectDropParam(() =>
                {
                    var bullet = new Bullet()
                    {
                        ReferenceBulletPallete = _selecting
                    };
                    return bullet;
                }));
                DragDrop.DoDragDrop(e.Source, dragData, DragDropEffects.Move);
                _draggingItem = false;
            }
        }

        public void OnMouseMoveAndDragNewBell(ActionExecutionContext e)
        {
            if (!_draggingItem)
                return;
            var arg = e.EventArgs as MouseEventArgs;

            Point mousePosition = arg.GetPosition(null);
            Vector diff = _mouseStartPosition - mousePosition;

            if (arg.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                Editor?.CancelBatchProjectilePlacement();
                var dragData = new DataObject(ToolboxDragDrop.DataFormat, new OngekiObjectDropParam(() =>
                {
                    var bullet = new Bell()
                    {
                        ReferenceBulletPallete = _selecting
                    };
                    return bullet;
                }));
                DragDrop.DoDragDrop(e.Source, dragData, DragDropEffects.Move);
                _draggingItem = false;
            }
        }

        public void OnMouseLeftButtonDown(ActionExecutionContext e)
        {
            var arg = e.EventArgs as MouseEventArgs;
            if ((arg.LeftButton != MouseButtonState.Pressed) || e.Source?.DataContext is not BulletPallete pallete)
                return;

            _mouseStartPosition = arg.GetPosition(null);
            _selecting = pallete;
            _draggingItem = true;
        }

        public void OnCopyNewBPL(ActionExecutionContext e)
        {
            var arg = e.EventArgs as MouseEventArgs;
            if ((arg.LeftButton != MouseButtonState.Pressed) || e.Source?.DataContext is not BulletPallete pallete)
                return;

            var cpBPL = new BulletPallete();
            cpBPL.Copy(pallete);
            cpBPL.StrID = null;

            Editor.Fumen.AddObject(cpBPL);
            DataView?.Refresh();
        }

        public void OnChangeEditorAxuiliaryLineColor(ActionExecutionContext e)
        {
            if (e.Source?.DataContext is not BulletPallete pallete)
                return;

            var dialog = new CommonColorPicker(
                () => Color.FromArgb(pallete.EditorAxuiliaryLineColor.A, pallete.EditorAxuiliaryLineColor.R, pallete.EditorAxuiliaryLineColor.G, pallete.EditorAxuiliaryLineColor.B),
                color => pallete.EditorAxuiliaryLineColor = OngekiFumenEditor.Base.ValueTypes.Color.FromArgb(color.A, color.R, color.G, color.B),
                Resources.ChangeAxuiliaryLineColor.Format(pallete.StrID));
            dialog.Show();
        }

        public void OnItemDoubleClick(BulletPallete e)
        {
            if (e is null)
                return;

            Editor.TryCancelAllObjectSelecting();
            Editor.Fumen.Bells
                .OfType<IBulletPalleteReferencable>()
                .Concat(Editor.Fumen.Bullets)
                .Where(x => x.ReferenceBulletPallete == e)
                .OfType<ISelectableObject>()
                .ForEach(x => x.IsSelected = true);

            IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
        }

        public void OnRefreshFilter()
        {
            if (DataView is ICollectionView view)
            {
                view.Filter = string.IsNullOrWhiteSpace(Filter) ? null : (r) =>
                {
                    if (r is BulletPallete p)
                        return p.ToString().Contains(Filter, StringComparison.InvariantCultureIgnoreCase);
                    return false;
                };
            }
        }
    }
}
