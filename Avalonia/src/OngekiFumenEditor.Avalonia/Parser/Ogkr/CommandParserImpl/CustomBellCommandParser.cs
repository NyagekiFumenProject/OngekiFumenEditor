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
    public class CustomBellCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => Bell.CustomCommandName;

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            //sb.AppendLine($"{idName}\t{u.TGrid.Serialize()}\t{u.XGrid.Serialize()}\t{shoot}\t{u.PlaceOffset}\t{target}\t{u.Speed}\t{size}\t{u.RandomOffsetRange}");

            var dataArr = args.GetDataArray<float>();
            var bell = new Bell();

            bell.ReferenceBulletPallete = BulletPallete.DummyCustomPallete;

            bell.TGrid.Unit = dataArr[1];
            bell.TGrid.Grid = (int)dataArr[2];
            bell.XGrid.Unit = dataArr[3];

            var shoot = args.GetData<string>(4)?.ToUpper();
            bell.ShooterValue = shoot switch
            {
                "UPS" => Shooter.TargetHead,
                "ENE" => Shooter.Enemy,
                "CEN" => Shooter.Center,
                _ => WarnAndDefault("ShooterValue", shoot, Shooter.TargetHead),
            };

            bell.PlaceOffset = args.GetData<int>(5);

            var target = args.GetData<string>(6)?.ToUpper();
            bell.TargetValue = target switch
            {
                "PLR" => Target.Player,
                "FIX" => Target.FixField,
                _ => WarnAndDefault("TargetValue", target, Target.Player),
            };

            bell.Speed = args.GetData<float>(7);

            var size = args.GetData<string>(8)?.ToUpper();
            bell.SizeValue = size switch
            {
                "N" => BulletSize.Normal,
                "L" => BulletSize.Large,
                _ => WarnAndDefault("SizeValue", size, BulletSize.Normal),
            };

            bell.RandomOffsetRange = args.GetData<int>(10);

            return bell;
        }

        private static T WarnAndDefault<T>(string fieldName, string raw, T fallback) where T : struct
        {
            Log.LogWarn($"Unknown custom bell {fieldName} '{raw}', fallback to '{fallback}'.");
            return fallback;
        }
    }
}


