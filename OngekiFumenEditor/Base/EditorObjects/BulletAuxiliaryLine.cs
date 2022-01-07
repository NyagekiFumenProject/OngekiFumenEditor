using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public class BulletAuxiliaryLine : ConnectorLineBase<Bullet>
    {
        public override Type ModelViewType => typeof(BulletAuxiliaryLineViewModel);

        public BulletAuxiliaryLine(Bullet bullet)
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
