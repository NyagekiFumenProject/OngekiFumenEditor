using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.EditorObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OngekiFumenEditor.Base.EditorObjects
{
    public abstract class LaneConnector : ConnectorLineBase<ConnectableObjectBase>
    {
        public abstract Color LineColor { get; }
        public override Type ModelViewType => typeof(LaneConnectorViewModel);
    }

    public class LaneLeftConnector : LaneConnector
    {
        static readonly Color DefaultColor = Colors.Red;
        public override Color LineColor => DefaultColor;
    }

    public class LaneCenterConnector : LaneConnector
    {
        static readonly Color DefaultColor = Colors.Green;
        public override Color LineColor => DefaultColor;
    }

    public class LaneRightConnector : LaneConnector
    {
        static readonly Color DefaultColor = Colors.Blue;
        public override Color LineColor => DefaultColor;
    }

    public class WallLeftConnector : LaneConnector
    {
        static readonly Color DefaultColor = Colors.HotPink;
        public override Color LineColor => DefaultColor;
    }

    public class WallRightConnector : LaneConnector
    {
        static readonly Color DefaultColor = Colors.HotPink;
        public override Color LineColor => DefaultColor;
    }

    public class LaneColorFulConnector : LaneConnector
    {
        public override Color LineColor => RefStart.ColorId.Color;
        public ColorfulLaneStart RefStart { get; set; }
    }
}
