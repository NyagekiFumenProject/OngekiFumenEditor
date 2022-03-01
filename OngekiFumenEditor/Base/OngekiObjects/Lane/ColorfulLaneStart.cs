using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
    public class ColorfulLaneStart : LaneStartBase
    {
        public override string IDShortName => "CLS";
        public override Type ModelViewType => typeof(LaneColorfulStartViewModel);

        public override LaneType LaneType => LaneType.Colorful;

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to) => new LaneCenterConnector()
        {
            From = from,
            To = to
        };
    }
}
