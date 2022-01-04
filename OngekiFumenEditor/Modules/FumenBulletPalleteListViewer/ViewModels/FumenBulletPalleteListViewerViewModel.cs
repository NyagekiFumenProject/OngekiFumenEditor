using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenBulletPalleteListViewer;
using OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Views;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
    }
}
