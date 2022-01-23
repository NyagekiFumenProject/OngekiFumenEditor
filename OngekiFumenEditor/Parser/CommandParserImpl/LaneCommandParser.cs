using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
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
    public abstract class LaneCommandParserBase : CommandParserBase
    {
        public void CommonParse(ConnectableObjectBase beam, CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();

            //todo add BeamTrack
            beam.TGrid = new TGrid(dataArr[2], (int)dataArr[3]);
            beam.XGrid = new XGrid(dataArr[4]);
        }
    }

    public abstract class LaneStartCommandParser<T> : LaneCommandParserBase where T : LaneStartBase, new()
    {
        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            var beam = new T()
            {
                RecordId = beamRecordId
            };

            CommonParse(beam, args, fumen);
            return beam;
        }
    }

    public abstract class LaneChildObjectCommandParser<T> : LaneCommandParserBase where T : ConnectableChildObjectBase, new()
    {
        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var beamRecordId = args.GetData<int>(1);
            if (fumen.Lanes.FirstOrDefault(x => x.RecordId == beamRecordId) is not ConnectableStartObject beamStart)
            {
                Log.LogError($"Can't parse {CommandLineHeader} command because beam record id not found : {beamRecordId}");
                return default;
            }

            var beam = new T();
            CommonParse(beam, args, fumen);
            beamStart.AddChildObject(beam);
            return beam;
        }
    }

    #region Implements

    [Export(typeof(ICommandParser))]
    public class LaneLeftStartCommandParser : LaneStartCommandParser<LaneLeftStart>
    {
        public override string CommandLineHeader => "LLS";
    }

    [Export(typeof(ICommandParser))]
    public class LaneCenterStartCommandParser : LaneStartCommandParser<LaneCenterStart>
    {
        public override string CommandLineHeader => "LCS";
    }

    [Export(typeof(ICommandParser))]
    public class LaneRightStartCommandParser : LaneStartCommandParser<LaneRightStart>
    {
        public override string CommandLineHeader => "LRS";
    }

    [Export(typeof(ICommandParser))]
    public class LaneLeftNextCommandParser : LaneChildObjectCommandParser<LaneLeftNext>
    {
        public override string CommandLineHeader => "LLN";
    }

    [Export(typeof(ICommandParser))]
    public class LaneCenterNextCommandParser : LaneChildObjectCommandParser<LaneCenterNext>
    {
        public override string CommandLineHeader => "LCN";
    }

    [Export(typeof(ICommandParser))]
    public class LaneRightNextCommandParser : LaneChildObjectCommandParser<LaneRightNext>
    {
        public override string CommandLineHeader => "LRN";
    }

    [Export(typeof(ICommandParser))]
    public class LaneLeftEndCommandParser : LaneChildObjectCommandParser<LaneLeftEnd>
    {
        public override string CommandLineHeader => "LLE";
    }

    [Export(typeof(ICommandParser))]
    public class LaneCenterEndCommandParser : LaneChildObjectCommandParser<LaneCenterEnd>
    {
        public override string CommandLineHeader => "LCE";
    }

    [Export(typeof(ICommandParser))]
    public class LaneRightEndCommandParser : LaneChildObjectCommandParser<LaneRightEnd>
    {
        public override string CommandLineHeader => "LRE";
    }

    #endregion
}
