using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Parser.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
    public class EnemySetCommandParser : ICommandParser
    {
        public string CommandLineHeader => CommandName;

        public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();
            var set = new EnemySet();

            set.TGrid.Unit = dataArr[1];
            set.TGrid.Grid = (int)dataArr[2];

            set.TagTblValue = args.GetData<string>(3)?.ToUpper() switch
            {
                "BOSS" => WaveChangeConst.Boss,
                "WAVE2" => WaveChangeConst.Wave2,
                "WAVE1" => WaveChangeConst.Wave1,
                _ => throw new NotSupportedException(),
            };

            return set;
        }
    }
}
