using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.Ogkr.CommandParserImpl
{
    public abstract class BeamCommandParserBase : CommandParserBase
    {
        /// <summary>
        /// 解析光束的 TGrid/XGrid/宽度档位；斜向光束另有第 6 列 shootPosDiff。
        /// 返回 false 表示该记录连几何列都不够，调用方应跳过它。
        /// </summary>
        public bool CommonParse(ConnectableObjectBase beam, CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();
            var ob = (IBeamObject)beam;
            var isOblique = CommandLineHeader is "OBS" or "OBN" or "OBE";
            var requiredColumns = isOblique ? 7 : 6;

            // 游戏侧 widthId 固定在第 5 列、斜向光束的 shootPosDiff 固定在第 6 列：缺列即越界崩溃。
            if (dataArr.Length < 6)
            {
                ReportColumnTooFew(fumen, args, requiredColumns, dataArr.Length);
                return false;
            }

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
            else if (isOblique)
            {
                ReportColumnTooFew(fumen, args, requiredColumns, dataArr.Length);
            }

            return true;
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

            if (!CommonParse(beam, args, fumen))
                return default;

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
                ReportMissingLaneStart(fumen, args, beamRecordId);
                return default;
            }

            var beam = new BeamNext();
            if (!CommonParse(beam, args, fumen))
                return default;

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

