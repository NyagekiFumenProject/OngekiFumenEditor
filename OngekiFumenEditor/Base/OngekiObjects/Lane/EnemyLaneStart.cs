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
    public class EnemyLaneStart : LaneStartBase
    {
        public override string IDShortName => "ENS";

        public override LaneType LaneType => LaneType.Enemy;

        public override Type NextType => typeof(EnemyLaneNext);
        public override Type EndType => typeof(EnemyLaneEnd);

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to) => new EnemyLaneConnector()
        {
            From = from,
            To = to
        };
    }
}
