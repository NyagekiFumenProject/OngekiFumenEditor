using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[Export(typeof(INyagekiCommandParser))]
	public class BulletCommandParser : INyagekiCommandParser
	{
		public string CommandName => "Bullet";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//Bullet:{bullet.ReferenceBulletPallete?.StrID}:X[{bullet.XGrid.Unit},{bullet.XGrid.Grid}],T[{bullet.TGrid.Unit},{bullet.TGrid.Grid}],D[{bullet.BulletDamageTypeValue}]
			var bullet = new Bullet();
			var data = seg[1].Split(":");

			var strId = data[0].Trim();
			bullet.ReferenceBulletPallete = fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == strId);

			using var d = data[1].GetValuesMapWithDisposable(out var map);
			bullet.TGrid = map["T"].ParseToTGrid();
			bullet.XGrid = map["X"].ParseToXGrid();
			bullet.BulletDamageTypeValue = Enum.Parse<Bullet.BulletDamageType>(map["D"]);

			fumen.AddObject(bullet);
		}
	}
}
