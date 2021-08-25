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
            public readonly static WaveChangeConst Wave1 = new WaveChangeConst("WAVE1");
            
            public readonly static WaveChangeConst Wave2 = new WaveChangeConst("WAVE2");
          
            public readonly static WaveChangeConst Boss = new WaveChangeConst("BOSS");

            public WaveChangeConst(string value) : base(value)
            {

            }
        }

        public WaveChangeConst TagTblValue { get; set; } = WaveChangeConst.Boss;

        public static string CommandName => "EST";
        public override string IDShortName => CommandName;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {TagTblValue}";
        }
    }
}
