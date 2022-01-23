using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl
{
    public abstract class BeamCommandParserBase : CommandParserBase
    {
        public void CommonParse(BeamBase beam, CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();

            //todo add BeamTrack
            beam.TGrid = new TGrid(dataArr[2], (int)dataArr[3]);
            beam.XGrid = new XGrid(dataArr[4]);
            beam.WidthId = (int)dataArr[5];
        }
    }

    [Export(typeof(ICommandParser))]
    public class BeamStartCommandParser : BeamCommandParserBase
    {
        public override string CommandLineHeader => "BMS";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            var beam = new BeamStart()
            {
                RecordId = beamRecordId
            };
            //fumen.AddObject(beam);

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
            if (fumen.Beams.FirstOrDefault(x => x.RecordId == beamRecordId) is not BeamStart beamStart)
            {
                Log.LogError($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
                return default;
            }

            var beam = new BeamNext();
            CommonParse(beam, args, fumen);
            beamStart.AddChildBeamObject(beam);
            return beam;
        }
    }

    [Export(typeof(ICommandParser))]
    public class BeamEndCommandParser : BeamCommandParserBase
    {
        public override string CommandLineHeader => "BME";

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            if (fumen.Beams.FirstOrDefault(x => x.RecordId == beamRecordId) is not BeamStart beamStart)
            {
                Log.LogError($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
                return default;
            }

            var beam = new BeamEnd();
            CommonParse(beam, args, fumen);
            beamStart.AddChildBeamObject(beam);
            return beam;
        }
    }
}
