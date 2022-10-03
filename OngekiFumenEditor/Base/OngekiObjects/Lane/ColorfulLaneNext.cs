using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Lane
{
    public class ColorfulLaneNext : LaneNextBase, IColorfulLane
    {
        public override string IDShortName => "CLN";

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
