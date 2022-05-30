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
    public class EnemySetCommandParser : INyagekiCommandParser
    {
        public string CommandName => "EnemySet";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            var eme = new EnemySet();
            var data = seg[1].Split(":");

            eme.TagTblValue = Enum.Parse<EnemySet.WaveChangeConst>(data[0]);
            eme.TGrid = data[1].ParseToTGrid();

            fumen.AddObject(eme);
        }
    }
}
