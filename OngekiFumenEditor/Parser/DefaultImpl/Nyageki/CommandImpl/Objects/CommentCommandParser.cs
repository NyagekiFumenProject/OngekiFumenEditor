using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Globalization;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
    [Export(typeof(INyagekiCommandParser))]
    public class CommentCommandParser : INyagekiCommandParser
    {
        public string CommandName => "Comment";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            var comment = new Comment();
            var data = seg[1].Split(":");

            var s = data[0];
            comment.Content = string.IsNullOrWhiteSpace(s) ? string.Empty : Base64.Decode(s);
            comment.TGrid = data[1].ParseToTGrid();
            if (data.Length > 2)
                comment.Color = Color.FromArgb(int.Parse(data[2].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

            fumen.AddObject(comment);
        }
    }
}

