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
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.UI.Dialogs;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
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
            Fumen = null;
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

        public void OnMouseMove(ActionExecutionContext e)
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
                    var bulletViewModel = new BulletViewModel();
                    var bullet = new Bullet()
                    {
                        ReferenceBulletPallete = selectingPallete
                    };
                    bulletViewModel.ReferenceOngekiObject = bullet;
                    return bulletViewModel;
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

        public void OnChangeEditorAxuiliaryLineColor(ActionExecutionContext e)
        {
            if (e.Source?.DataContext is not BulletPallete pallete)
                return;

            var dialog = new CommonColorPicker(() => pallete.EditorAxuiliaryLineColor, color => pallete.EditorAxuiliaryLineColor = color);
            dialog.Show();
        }
    }
}
