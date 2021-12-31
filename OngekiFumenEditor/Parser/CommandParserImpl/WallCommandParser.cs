using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Wall;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl
{
    public abstract class WallCommandParserBase : ICommandParser
    {
        public abstract string CommandLineHeader { get; }

        public void CommonParse(WallBase beam, CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();

            //todo add BeamTrack
            beam.TGrid = new TGrid(dataArr[2], (int)dataArr[3]);
            beam.XGrid = new XGrid(dataArr[4]);
        }

        public abstract OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen);
    }

    [Export(typeof(ICommandParser))]
    public class WallStartCommandParser : WallCommandParserBase
    {
        public override string CommandLineHeader => "WLS";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            var beam = new WallStart()
            {
                RecordId = beamRecordId
            };
            //fumen.AddObject(beam);

            CommonParse(beam, args, fumen);

            return beam;
        }
    }

    [Export(typeof(ICommandParser))]
    public class WallNextCommandParser : WallCommandParserBase
    {
        public override string CommandLineHeader => "WLN";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            if (fumen.Walls.FirstOrDefault(x => x.RecordId == beamRecordId) is not WallStart beamStart)
            {
                Log.LogError($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
                return default;
            }

            var beam = new WallNext();
            CommonParse(beam, args, fumen);
            beamStart.AddChildWallObject(beam);
            return beam;
        }
    }

    [Export(typeof(ICommandParser))]
    public class WallEndCommandParser : WallCommandParserBase
    {
        public override string CommandLineHeader => "WLE";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            if (fumen.Walls.FirstOrDefault(x => x.RecordId == beamRecordId) is not WallStart beamStart)
            {
                Log.LogError($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
                return default;
            }

            var beam = new WallEnd();
            CommonParse(beam, args, fumen);
            beamStart.AddChildWallObject(beam);
            return beam;
        }
    }
}
