using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class EnemySet : OngekiTimelineObjectBase
    {
        public class WaveChangeConst : FadeStringEnum
        {
            public static WaveChangeConst Wave1 { get; } = new WaveChangeConst("WAVE1");

            public static WaveChangeConst Wave2 { get; } = new WaveChangeConst("WAVE2");

            public static WaveChangeConst Boss { get; } = new WaveChangeConst("BOSS");

            public WaveChangeConst(string value) : base(value)
            {

            }
        }

        private WaveChangeConst tagTblValue = WaveChangeConst.Boss;
        public WaveChangeConst TagTblValue
        {
            get { return tagTblValue; }
            set
            {
                tagTblValue = value;
                NotifyOfPropertyChange(() => TagTblValue);
            }
        }

        public static string CommandName => "EST";
        public override string IDShortName => CommandName;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {TagTblValue}";
        }
    }
}
