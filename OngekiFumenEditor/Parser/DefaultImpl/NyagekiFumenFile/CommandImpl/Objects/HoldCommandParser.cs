using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.NyagekiFumenFile.CommandImpl.Objects
{
    [Export(typeof(INyagekiCommandParser))]
    public class HoldCommandParser : INyagekiCommandParser
    {
        public string CommandName => "Hold";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            //$"Hold:{hold.ReferenceLaneStrId},{hold.IsCritical}:(X[{hold.XGrid.Unit},{hold.XGrid.Grid}],T[{hold.TGrid.Unit},{hold.TGrid.Grid}]) -> (X[{end.XGrid.Unit},{end.XGrid.Grid}],T[{end.TGrid.Unit},{end.TGrid.Grid}])"

            var data = seg[1].Split(":");

            var commData = data[0].Split(",");
            var refRecordId = int.Parse(commData[0]);
            var refLane = fumen.Lanes.FirstOrDefault(x => x.RecordId == refRecordId);
            if (refLane is null)
                throw new Exception($"Can't parse line as Hold/WallHold because reference lane ({refRecordId}) is not found.");

            var hold = refLane.IsWallLane ? new WallHold() : new Hold();
            hold.ReferenceLaneStart = refLane;
            hold.IsCritical = bool.Parse(commData[1]);

            var maps = data[1].Split("->").Select(x => x.Trim().TrimStart('(').TrimEnd(')')).Select(x => (x.GetValuesMapWithDisposable(out var d), d)).ToArray();
            var notes = maps.Select(x => x.d).ToArray();

            hold.TGrid = notes[0]["T"].ParseToTGrid();
            hold.XGrid = notes[0]["X"].ParseToXGrid();

            if (notes.Length > 1)
            {
                var end = refLane.IsWallLane ? new WallHoldEnd() : new HoldEnd();
                end.TGrid = notes[1]["T"].ParseToTGrid();
                end.XGrid = notes[1]["X"].ParseToXGrid();

                hold.AddChildObject(end);
            }

            foreach ((var d, _) in maps)
                d.Dispose();

            fumen.AddObject(hold);
        }
    }
}
