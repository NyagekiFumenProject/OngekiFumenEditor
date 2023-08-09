using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
    [Export(typeof(INyagekiCommandParser))]
    public class CommentCommandParser : INyagekiCommandParser
    {
        public string CommandName => "Comment";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            //$"Comment\t:\t{comment.Content}\t:\tT[{comment.TGrid.Unit},{comment.TGrid.Grid}]"
            var bpm = new Comment();
            var data = seg[1].Split(":");

            var s = data[0];
            bpm.Content = string.IsNullOrWhiteSpace(s) ? string.Empty : Base64.Decode(s);
            bpm.TGrid = data[1].ParseToTGrid();

            fumen.AddObject(bpm);
        }
    }
}
