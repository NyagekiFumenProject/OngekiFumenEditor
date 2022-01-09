using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
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
    public class TapCommandParser : ICommandParser
    {
        public virtual string CommandLineHeader => "TAP";

        public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();
            var tap = new Tap();

            tap.IsCritical = args.GetData<string>(0) == "CTP" || args.GetData<string>(0) == "XTP";

            tap.TGrid.Unit = dataArr[2];
            tap.TGrid.Grid = (int)dataArr[3];
            tap.XGrid.Unit = dataArr[4];
            tap.XGrid.Grid = (int)dataArr[5];

            var laneId = args.GetData<int>(1);
            var refLaneStart = fumen.Lanes.FirstOrDefault(x => x.RecordId == laneId);
            if (refLaneStart is null)
            {
                Log.LogWarn($"Tap parse can't find lane RecordId = {laneId}");
            }

            tap.ReferenceLaneStart = refLaneStart;
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
