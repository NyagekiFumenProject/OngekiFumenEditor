using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
	[Export(typeof(ICommandParser))]
	public class SoflanCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => "SFL";

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var sfl = new Soflan();
			Apply(sfl, args);
			return sfl;
		}

		public void Apply(ISoflan sfl, CommandArgs args)
		{
			var dataArr = args.GetDataArray<float>();
			sfl.TGrid.Unit = dataArr[1];
			sfl.TGrid.Grid = (int)dataArr[2];

			var length = (int)dataArr[3];
			sfl.EndTGrid = sfl.TGrid + new GridOffset(0, length);

			sfl.Speed = dataArr[4];
		}
	}

	[Export(typeof(ICommandParser))]
	public class KeyframeSoflanCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => "[KEY_SFL]";

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var sfl = new KeyframeSoflan();
			var dataArr = args.GetDataArray<float>();
			sfl.TGrid.Unit = dataArr[1];
			sfl.TGrid.Grid = (int)dataArr[2];
			sfl.Speed = dataArr[3];
			return sfl;
		}
	}

	[Export(typeof(ICommandParser))]
	public class InterpolatableSoflanCommandParser : SoflanCommandParser
	{
		public override string CommandLineHeader => "[INTP_SFL]";

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var sfl = new InterpolatableSoflan();
			Apply(sfl, args);

			var dataArr = args.GetDataArray<float>();
			sfl.Easing = Enum.Parse<EasingTypes>(args.GetData<string>(5));
			((InterpolatableSoflan.InterpolatableSoflanIndicator)sfl.EndIndicator).Speed = dataArr[6];

			return sfl;
		}
	}
}
