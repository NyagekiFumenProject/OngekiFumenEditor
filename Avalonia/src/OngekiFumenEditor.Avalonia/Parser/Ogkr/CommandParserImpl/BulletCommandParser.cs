using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using System;
using System.Linq;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
{
	[RegisterSingleton<ICommandParser>]
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
                _ => WarnAndDefault(type, BulletDamageType.Normal),
            };

			return bullet;
		}

        private static BulletDamageType WarnAndDefault(string raw, BulletDamageType fallback)
        {
            Log.LogWarn($"Unknown bullet damage type '{raw}', fallback to '{fallback}'.");
            return fallback;
        }
    }
}


