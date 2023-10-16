using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.Toolbox;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Views;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser.ViewModels
{
    [Export(typeof(IFumenBulletPalleteListViewer))]
    public class FumenBulletPalleteListViewerViewModel : Tool, IFumenBulletPalleteListViewer
    {
        public FumenBulletPalleteListViewerViewModel()
        {
            DisplayName = "子弹管理";
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Fumen = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor?.Fumen;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Fumen = @new?.Fumen;
            this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
                Fumen = (sender as FumenVisualEditorViewModel).Fumen;
        }

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        private OngekiFumen fumen;
        public OngekiFumen Fumen
        {
            get
            {
                return fumen;
            }
            set
            {
                fumen = value;
                NotifyOfPropertyChange(() => Fumen);
                NotifyOfPropertyChange(() => IsEnable);
            }
        }

        public bool IsEnable => Fumen is not null;

        private BulletPallete selectingPallete;
        public BulletPallete SelectingPallete
        {
            get
            {
                return selectingPallete;
            }
            set
            {
                selectingPallete = value;
                NotifyOfPropertyChange(() => SelectingPallete);
            }
        }

        public void OnCreateNew()
        {
            var plattele = new BulletPallete();
            Fumen.AddObject(plattele);
        }

        public void OnDeleteSelecting(FumenBulletPalleteListViewerView e)
        {
            if (SelectingPallete is not null)
            {
                Fumen.RemoveObject(SelectingPallete);
            }
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
                var dragData = new DataObject(ToolboxDragDrop.DataFormat, new OngekiObjectDropParam(() =>
                {
                    var bullet = new Bullet()
                    {
                        ReferenceBulletPallete = selectingPallete
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
                var dragData = new DataObject(ToolboxDragDrop.DataFormat, new OngekiObjectDropParam(() =>
                {
                    var bullet = new Bell()
                    {
                        ReferenceBulletPallete = selectingPallete
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

            Fumen.AddObject(cpBPL);
        }

        public void OnChangeEditorAxuiliaryLineColor(ActionExecutionContext e)
        {
            if (e.Source?.DataContext is not BulletPallete pallete)
                return;

            var dialog = new CommonColorPicker(() => pallete.EditorAxuiliaryLineColor, color => pallete.EditorAxuiliaryLineColor = color, $"变更 {pallete.StrID} 辅助线颜色");
            dialog.Show();
        }
    }
}
