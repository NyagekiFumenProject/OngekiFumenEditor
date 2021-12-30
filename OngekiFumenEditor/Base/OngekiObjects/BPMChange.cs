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
            var size = this.BPM / 240 * timeGridSize;
            var unit = (int)(len / size);
            var grid = (int)(len % size / size * TGrid.ResT);

            return new GridOffset(unit, grid);
        }
    }
}
