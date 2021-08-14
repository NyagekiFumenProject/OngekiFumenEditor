using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class EnemySet : IOngekiObject, ITimelineObject
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

        public TGrid TGrid { get; set; }
        public WaveChangeConst tagTblValue { get; set; }

        public string Group => "COMPOSITION";

        public string IDShortName => "EST";

        public string Name => "EnemySoundSet";
    }
}
