using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
	[Export(typeof(ICommandParser))]
	public class BulletCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => Bullet.CommandName;

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<float>();
			var bullet = new Bullet();

			var palleteId = args.GetData<string>(1);
			bullet.ReferenceBulletPallete = fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == palleteId);
			bullet.TGrid.Unit = dataArr[2];
			bullet.TGrid.Grid = (int)dataArr[3];
			bullet.XGrid.Unit = dataArr[4];

			var type = args.GetData<string>(5)?.ToUpper();
			bullet.BulletDamageTypeValue = type switch
			{
				"NML" => BulletDamageType.Normal,
				"STR" => BulletDamageType.Hard,
				"DNG" => BulletDamageType.Danger,
				_ => throw new NotImplementedException($"BulletDamageTypeValue = {type}"),
			};

			return bullet;
		}
	}
}
