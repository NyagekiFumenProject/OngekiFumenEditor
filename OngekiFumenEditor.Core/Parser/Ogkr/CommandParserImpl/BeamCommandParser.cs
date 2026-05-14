using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Utils;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Core.Parser.Ogkr.CommandParserImpl
{
    public abstract class BeamCommandParserBase : CommandParserBase
    {
        public void CommonParse(ConnectableObjectBase beam, CommandArgs args)
        {
            var dataArr = args.GetDataArray<float>();
            var ob = (IBeamObject)beam;

            beam.TGrid = new TGrid(dataArr[2], (int)dataArr[3]);
            beam.XGrid = new XGrid(dataArr[4]);
            ob.WidthId = WidthId.ParseFromId((int)dataArr[5]);

            if (dataArr.Length > 6)
            {
                var xUnit = dataArr[6];
                var xGrid = new XGrid(xUnit, 0);
                xGrid.NormalizeSelf();
                ob.ObliqueSourceXGridOffset = xGrid;
            }
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
                RecordId = beamRecordId,
            };

            CommonParse(beam, args);

            return beam;
        }
    }


    [Export(typeof(ICommandParser))]
    public class ObliqueBeamStartCommandParser : BeamStartCommandParser
    {
        public override string CommandLineHeader => "OBS";
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
                CoreLog.LogError($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
                return default;
            }

            var beam = new BeamNext();
            CommonParse(beam, args);
            beamStart.AddChildObject(beam);
            return beam;
        }
    }

    [Export(typeof(ICommandParser))]
    public class ObliqueBeamNextCommandParser : BeamNextCommandParser
    {
        public override string CommandLineHeader => "OBN";
    }

    [Export(typeof(ICommandParser))]
    public class BeamEndCommandParser : BeamNextCommandParser
    {
        public override string CommandLineHeader => "BME";
    }

    [Export(typeof(ICommandParser))]
    public class ObliqueBeamEndCommandParser : BeamEndCommandParser
    {
        public override string CommandLineHeader => "OBE";
    }
}

