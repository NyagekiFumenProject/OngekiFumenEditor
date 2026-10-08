using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Parser.Ogkr;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Globalization;

namespace OngekiFumenEditor.Parser.Ogkr.CommandParserImpl.Editor
{
    [Export(typeof(ICommandParser))]
    public class CommentCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => Comment.CommandName;

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            var dataArr = args.GetDataArray<float>();
            var cmt = new Comment();

            cmt.TGrid.Unit = dataArr[1];
            cmt.TGrid.Grid = (int)dataArr[2];
            var s = args.GetData<string>(3);
            cmt.Content = string.IsNullOrWhiteSpace(s) ? string.Empty : Base64.Decode(s);
            var color = args.GetData<string>(4);
            if (color is not null)
                cmt.Color = Color.FromArgb(int.Parse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

            return cmt;
        }
    }
}
