using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class BulletPalleteAuxiliaryLine : ConnectorLineBase<IBulletPalleteReferencable>
    {
        public override Type ModelViewType => typeof(BulletPalleteAuxiliaryLineViewModel);
        
        private Visibility visibility = Visibility.Visible;
        public Visibility Visibility
        {
            get => visibility;
            set => Set(ref visibility, value);
        }

        public BulletPalleteAuxiliaryLine(IBulletPalleteReferencable bullet)
        {
            To = bullet;
            From = bullet;
        }

        public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return To.CheckVisiable(minVisibleTGrid, maxVisibleTGrid);
        }
    }
}
