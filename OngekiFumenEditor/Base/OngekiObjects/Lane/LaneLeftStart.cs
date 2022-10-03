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
    public class LaneLeftStart : LaneStartBase
    {
        public override string IDShortName => "LLS";

        public override LaneType LaneType => LaneType.Left;
        public override Type NextType => typeof(LaneLeftNext);
        public override Type EndType => typeof(LaneLeftEnd);

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to) => GenerateConnectorInternal<LaneLeftConnector>(from, to);
    }
}
