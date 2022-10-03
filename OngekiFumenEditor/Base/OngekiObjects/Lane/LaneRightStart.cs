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
    public class LaneRightStart : LaneStartBase
    {
        private ColorId colorId = default;
        public ColorId ColorId
        {
            get => colorId;
            set => Set(ref colorId, value);
        }

        public override string IDShortName => "LRS";

        public override Type NextType => typeof(LaneRightNext);
        public override Type EndType => typeof(LaneRightEnd);

        public override LaneType LaneType => LaneType.Right;

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to) => GenerateConnectorInternal<LaneRightConnector>(from, to);
    }
}
