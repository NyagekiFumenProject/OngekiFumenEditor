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
    public class ColorfulLaneStart : LaneStartBase, IColorfulLane
    {
        public override string IDShortName => "CLS";
        public override Type ModelViewType => typeof(LaneColorfulStartViewModel);

        public override LaneType LaneType => LaneType.Colorful;

        private ColorId colorId = ColorIdConst.Akari;
        public ColorId ColorId
        {
            get => colorId;
            set => Set(ref colorId, value);
        }

        private int brightness = 0;
        public int Brightness
        {
            get => brightness;
            set => Set(ref brightness, value);
        }

        protected override ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to) => new LaneColorFulConnector()
        {
            From = from,
            To = to
        };
    }
}
