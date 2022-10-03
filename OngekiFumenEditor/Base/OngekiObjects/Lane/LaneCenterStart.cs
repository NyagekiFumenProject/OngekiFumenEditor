using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
    public class LaneCenterStart : LaneStartBase
    {
        public override string IDShortName => "LCS";

        public override LaneType LaneType => LaneType.Center;
        public override Type NextType => typeof(LaneCenterNext);
        public override Type EndType => typeof(LaneCenterEnd);

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to) => GenerateConnectorInternal<LaneCenterConnector>(from, to);
    }
}
