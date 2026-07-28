using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using System;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[RegisterSingleton<INyagekiCommandParser>]
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


