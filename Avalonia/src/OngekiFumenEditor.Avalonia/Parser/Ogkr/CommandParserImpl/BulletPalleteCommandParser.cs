using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using System;
using System.Linq;
using OngekiFumenEditor.Avalonia.Utils;
using static OngekiFumenEditor.Avalonia.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl
{
	[RegisterSingleton<ICommandParser>]
	public class BulletPalleteCommandParser : CommandParserBase
	{
		public override string CommandLineHeader => CommandName;

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			var dataIntArr = args.GetDataArray<int>();
            var dataFloatArr = args.GetDataArray<float>();
            var dataStrArr = args.GetDataArray<string>();
			var bpl = new BulletPallete();

			bpl.StrID = dataStrArr.ElementAtOrDefault(1);
			bpl.ShooterValue = dataStrArr.ElementAtOrDefault(2)?.ToUpper() switch
			{
				"UPS" => Shooter.TargetHead,
				"ENE" => Shooter.Enemy,
				"CEN" => Shooter.Center,
				_ => WarnAndDefault("Shooter", dataStrArr.ElementAtOrDefault(2), Shooter.TargetHead),
			};
			bpl.PlaceOffset = dataIntArr.ElementAtOrDefault(3);
			bpl.TargetValue = dataStrArr.ElementAtOrDefault(4)?.ToUpper() switch
			{
				"PLR" => Target.Player,
				"FIX" => Target.FixField,
				_ => WarnAndDefault("Target", dataStrArr.ElementAtOrDefault(4), Target.Player),
			};
			bpl.Speed = dataFloatArr.ElementAtOrDefault(5);
			bpl.SizeValue = dataStrArr.ElementAtOrDefault(6)?.ToUpper() switch
			{
				"L" => BulletSize.Large,
				"N" or _ => BulletSize.Normal,
			};
			bpl.TypeValue = dataStrArr.ElementAtOrDefault(7)?.ToUpper() switch
			{
				"SQR" => BulletType.Square,
				"NDL" => BulletType.Needle,
				"CIR" or _ => BulletType.Circle,
			};
            bpl.RandomOffsetRange = dataIntArr.ElementAtOrDefault(8);

            return bpl;
		}

        private static T WarnAndDefault<T>(string fieldName, string raw, T fallback) where T : struct
        {
            Log.LogWarn($"Unknown bullet pallete {fieldName} '{raw}', fallback to '{fallback}'.");
            return fallback;
        }
    }
}



