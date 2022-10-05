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

        public override Type NextType => typeof(ColorfulLaneNext);
        public override Type EndType => typeof(ColorfulLaneEnd);

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not ColorfulLaneStart cls)
                return;

            ColorId = cls.ColorId;
            Brightness = cls.Brightness;
        }
    }
}
