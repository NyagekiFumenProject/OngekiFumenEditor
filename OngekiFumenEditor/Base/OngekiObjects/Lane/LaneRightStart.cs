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
        public override string IDShortName => "LRS";
        public override LaneType LaneType => LaneType.Right;

        public override ConnectableNextObject CreateNextObject() => new LaneRightNext();
        public override ConnectableEndObject CreateEndObject() => new LaneRightEnd();
    }
}
