using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
	[RegisterSingleton<INyagekiCommandParser>]
	public class BellCommandParser : INyagekiCommandParser
	{
		public string CommandName => "Bell";

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			//$"Bell:{bell.ReferenceBulletPallete?.StrID}:X[{bell.XGrid.Unit},{bell.XGrid.Grid}],T[{bell.TGrid.Unit},{bell.TGrid.Grid}]"
			var bell = new Bell();
			var data = seg[1].Split(":");

			var strId = data[0].Trim();
			bell.ReferenceBulletPallete = fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == strId);

			using var d = data[1].GetValuesMapWithDisposable(out var map);
			bell.TGrid = map["T"].ParseToTGrid();
			bell.XGrid = map["X"].ParseToXGrid();

			fumen.AddObject(bell);
		}
	}
}


