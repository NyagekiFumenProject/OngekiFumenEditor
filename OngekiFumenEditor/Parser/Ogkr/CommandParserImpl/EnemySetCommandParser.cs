using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.ComponentModel.Composition;
using static OngekiFumenEditor.Base.OngekiObjects.EnemySet;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
	[Export(typeof(ICommandParser))]
	public class EnemySetCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => CommandName;

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
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
