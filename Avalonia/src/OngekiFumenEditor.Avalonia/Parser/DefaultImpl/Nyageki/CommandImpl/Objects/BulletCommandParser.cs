using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using System;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[RegisterSingleton<INyagekiCommandParser>]
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
			bullet.BulletDamageTypeValue = Enum.Parse<BulletDamageType>(map["D"]);

			fumen.AddObject(bullet);
		}
	}
}


