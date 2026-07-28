using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
{
	[RegisterSingleton<ICommandParser>]
	public class BellCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => Bell.CommandName;

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();
			var bell = new Bell();

			bell.TGrid.Unit = dataArr[1];
			bell.TGrid.Grid = (int)dataArr[2];
			bell.XGrid.Unit = dataArr[3];

			var palleteId = args.GetData<string>(4);
			if (!string.IsNullOrWhiteSpace(palleteId) && palleteId != "--")
				bell.ReferenceBulletPallete = fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == palleteId);

			return bell;
		}
	}
}


