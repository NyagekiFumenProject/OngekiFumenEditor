using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using OngekiFumenEditor.Parser;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
    public class TapCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => "TAP";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();

            var laneId = args.GetData<int>(1);
            var refLaneStart = fumen.Lanes.FirstOrDefault(x => x.RecordId == laneId);
            if (refLaneStart is null)
            {
                Log.LogWarn($"Tap parse can't find lane/wallLane RecordId = {laneId}");
            }
            var tap = (refLaneStart?.IsWallLane ?? (laneId == -2)) ? new WallTap() : new Tap();

            tap.ReferenceLaneStart = refLaneStart;

            tap.IsCritical = args.GetData<string>(0) == "CTP" || args.GetData<string>(0) == "XTP";

            tap.TGrid.Unit = dataArr[2];
            tap.TGrid.Grid = (int)dataArr[3];
            tap.XGrid.Unit = dataArr[4];
            tap.XGrid.Grid = (int)dataArr[5];
            return tap;
        }
    }

    [Export(typeof(ICommandParser))]
    public class CriticalTapCommandParser1 : TapCommandParser
    {
        public override string CommandLineHeader => "CTP";
    }

    [Export(typeof(ICommandParser))]
    public class CriticalTapCommandParser2 : TapCommandParser
    {
        public override string CommandLineHeader => "XTP";
    }
}
