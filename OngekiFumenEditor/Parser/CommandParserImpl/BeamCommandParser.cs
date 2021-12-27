using ExtrameFunctionCalculator;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Base.OngekiObjects.Beam;

namespace OngekiFumenEditor.Parser.CommandParserImpl
{
    public abstract class BeamCommandParserBase : ICommandParser
    {
        public abstract string CommandLineHeader { get; }

        public void CommonParse(Beam beam, CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();

            //todo add BeamTrack
            var track = new BeamTrack()
            {
                TGrid = new TGrid(dataArr[2], (int)dataArr[3]),
                XGrid = new XGrid(dataArr[4]),
                WidthId = (int)dataArr[5],
            };

            beam.Tracks.Add(track.TGrid, track);
        }

        public abstract OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen);
    }

    [Export(typeof(ICommandParser))]
    public class BeamStartCommandParser : BeamCommandParserBase
    {
        public override string CommandLineHeader => "BMS";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            var beam = new Beam()
            {
                RecordId = beamRecordId
            };
            fumen.AddObject(beam);

            CommonParse(beam, args, fumen);

            return beam;
        }
    }

    [Export(typeof(ICommandParser))]
    public class BeamNextCommandParser : BeamCommandParserBase
    {
        public override string CommandLineHeader => "BMN";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            if (!fumen.Beams.TryGetValue(beamRecordId ,out var beam))
            {
                Log.Error($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
                return default;
            }

            CommonParse(beam, args, fumen);

            return default;
        }
    }

    [Export(typeof(ICommandParser))]
    public class BeamEndCommandParser : BeamCommandParserBase
    {
        public override string CommandLineHeader => "BME";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            if (!fumen.Beams.TryGetValue(beamRecordId, out var beam))
            {
                Log.Error($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
                return default;
            }

            CommonParse(beam, args, fumen);

            return default;
        }
    }
}
