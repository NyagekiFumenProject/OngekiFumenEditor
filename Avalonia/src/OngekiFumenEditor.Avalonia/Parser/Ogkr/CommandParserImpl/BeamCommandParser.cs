using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Utils;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
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

            if (dataArr.TryElementAt(6, out var xUnit))
            {
                var xGrid = new XGrid(xUnit, 0);
                xGrid.NormalizeSelf();
                ob.ObliqueSourceXGridOffset = xGrid;
            }
        }
    }

    [RegisterSingleton<ICommandParser>]
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


    [RegisterSingleton<ICommandParser>]
    public class ObliqueBeamStartCommandParser : BeamStartCommandParser
    {
        public override string CommandLineHeader => "OBS";
    }

    [RegisterSingleton<ICommandParser>]
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
            CommonParse(beam, args);
            beamStart.AddChildObject(beam);
            return beam;
        }
    }

    [RegisterSingleton<ICommandParser>]
    public class ObliqueBeamNextCommandParser : BeamNextCommandParser
    {
        public override string CommandLineHeader => "OBN";
    }

    [RegisterSingleton<ICommandParser>]
    public class BeamEndCommandParser : BeamNextCommandParser
    {
        public override string CommandLineHeader => "BME";
    }

    [RegisterSingleton<ICommandParser>]
    public class ObliqueBeamEndCommandParser : BeamEndCommandParser
    {
        public override string CommandLineHeader => "OBE";
    }
}


