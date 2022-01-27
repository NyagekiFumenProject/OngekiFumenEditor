using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BPMChange : OngekiTimelineObjectBase
    {
        public override Type ModelViewType => typeof(BPMChangeViewModel);

        private double bpm = 240;
        public double BPM
        {
            get { return bpm; }
            set
            {
                bpm = value;
                NotifyOfPropertyChange(() => BPM);
            }
        }

        public static string CommandName => "BPM";
        public override string IDShortName => CommandName;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {BPM}";
        }

        public override string ToString() => Serialize(default);

        public GridOffset LengthConvertToOffset(double len, int timeGridSize)
        {
            var totalGrid = len * (TGrid.ResT * BPM) / 240000;

            var p = totalGrid / TGrid.ResT;
            var unit = (int)p;
            var grid = (int)((p - unit)  * TGrid.ResT);

            return new GridOffset(unit, grid);
        }

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not BPMChange fromBpm)
                return;

            BPM = fromBpm.BPM;
        }
    }
}
