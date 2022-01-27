using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class MeterChange : OngekiTimelineObjectBase
    {
        public override Type ModelViewType => typeof(MeterChangeViewModel);

        private int bunShi = 4;
        public int BunShi
        {
            get { return bunShi; }
            set
            {
                bunShi = value;
                NotifyOfPropertyChange(() => BunShi);
            }
        }

        private int bunbo = 4;
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

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            base.Copy(fromObj, fumen);

            if (fromObj is not MeterChange from)
                return;

            Bunbo = from.Bunbo;
            BunShi = from.BunShi;
        }
    }
}
