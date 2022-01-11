using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
    public class HoldCommandParser : ICommandParser
    {
        public virtual string CommandLineHeader => "HLD";

        public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();

            var laneId = args.GetData<int>(1);
            var refLaneStart = fumen.Lanes.FirstOrDefault(x => x.RecordId == laneId);
            if (refLaneStart is null)
            {
                Log.LogWarn($"Tap parse can't find lane/wallLane RecordId = {laneId}");
            }
            var hold = (refLaneStart?.IsWallLane ?? false) ? new WallHold() : new Hold();

            hold.ReferenceLaneStart = refLaneStart;

            hold.IsCritical = args.GetData<string>(0) == "CHD" || args.GetData<string>(0) == "XHD";

            hold.TGrid.Unit = dataArr[2];
            hold.TGrid.Grid = (int)dataArr[3];
            hold.XGrid.Unit = dataArr[4];
            hold.XGrid.Grid = (int)dataArr[5];

            var holdEnd = new HoldEnd();
            hold.AddChildObject(holdEnd);

            holdEnd.TGrid.Unit = dataArr[6];
            holdEnd.TGrid.Grid = (int)dataArr[7];
            holdEnd.XGrid.Unit = dataArr[8];
            holdEnd.XGrid.Grid = (int)dataArr[9];

            return hold;
        }
    }

    [Export(typeof(ICommandParser))]
    public class CriticalHoldCommandParser1 : HoldCommandParser
    {
        public override string CommandLineHeader => "CHD";
    }

    [Export(typeof(ICommandParser))]
    public class CriticalHoldCommandParser2 : HoldCommandParser
    {
        public override string CommandLineHeader => "XHD";
    }
}
