using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class MeterChange : OngekiTimelineObjectBase
    {
        private int bunShi = default;
        public int BunShi
        {
            get { return bunShi; }
            set
            {
                bunShi = value;
                NotifyOfPropertyChange(() => BunShi);
            }
        }

        private int bunbo = default;
        public int Bunbo
        {
            get { return bunbo; }
            set
            {
                bunbo = value;
                NotifyOfPropertyChange(() => Bunbo);
            }
        }

        public static string CommandName => "MET";
        public override string IDShortName => CommandName;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {BunShi} {Bunbo}";
        }
    }
}
