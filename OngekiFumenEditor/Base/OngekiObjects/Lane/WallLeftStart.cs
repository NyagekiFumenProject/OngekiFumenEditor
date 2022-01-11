using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Wall
{
    public class WallLeftStart : WallStartBase
    {
        public override string IDShortName => "WLS";
        public override Type ModelViewType => typeof(WallStartViewModel<WallLeftStart>);

        public override LaneType LaneType => LaneType.WallLeft;

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to) => new WallLeftConnector()
        {
            From = from,
            To = to
        };
    }
}
