using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
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
                _ => throw new NotImplementedException($"ShooterValue = {shoot}"),
            };

            bell.PlaceOffset = args.GetData<int>(5);

            var target = args.GetData<string>(6)?.ToUpper();
            bell.TargetValue = target switch
            {
                "PLR" => Target.Player,
                "FIX" => Target.FixField,
                _ => throw new NotImplementedException($"TargetValue = {target}"),
            };

            bell.Speed = args.GetData<float>(7);

            var size = args.GetData<string>(8)?.ToUpper();
            bell.SizeValue = size switch
            {
                "N" => BulletSize.Normal,
                "L" => BulletSize.Large,
                _ => throw new NotImplementedException($"SizeValue = {size}"),
            };

            bell.RandomOffsetRange = args.GetData<int>(10);

            return bell;
        }
    }
}
