using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
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
			cmt.Content = string.IsNullOrWhiteSpace(s) ? string.Empty : Base64.Decode(args.GetData<string>(3));

			return cmt;
		}
	}
}
