using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
    public class CustomBulletCommandParser : CommandParserBase
    {
        public override string CommandLineHeader => Bullet.CustomCommandName;

        public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            //sb.AppendLine($"{idName}\t{u.TGrid.Serialize()}\t{u.XGrid.Serialize()}\t{damage}\t{shoot}\t{u.PlaceOffset}\t{target}\t{u.Speed}\t{size}\t{type}\t{u.RandomOffsetRange}");
            var dataArr = args.GetDataArray<float>();
            var bullet = new Bullet();

            bullet.ReferenceBulletPallete = BulletPallete.DummyCustomPallete;

            bullet.TGrid.Unit = dataArr[1];
            bullet.TGrid.Grid = (int)dataArr[2];
            bullet.XGrid.Unit = dataArr[3];

            var damage = args.GetData<string>(4)?.ToUpper();
            bullet.BulletDamageTypeValue = damage switch
            {
                "NML" => BulletDamageType.Normal,
                "STR" => BulletDamageType.Hard,
                "DNG" => BulletDamageType.Danger,
                _ => throw new NotImplementedException($"BulletDamageTypeValue = {damage}"),
            };

            var shoot = args.GetData<string>(5)?.ToUpper();
            bullet.ShooterValue = shoot switch
            {
                "UPS" => Shooter.TargetHead,
                "ENE" => Shooter.Enemy,
                "CEN" => Shooter.Center,
                _ => throw new NotImplementedException($"ShooterValue = {shoot}"),
            };

            bullet.PlaceOffset = args.GetData<int>(6);

            var target = args.GetData<string>(7)?.ToUpper();
            bullet.TargetValue = target switch
            {
                "PLR" => Target.Player,
                "FIX" => Target.FixField,
                _ => throw new NotImplementedException($"TargetValue = {target}"),
            };

            bullet.Speed = args.GetData<float>(8);

            var size = args.GetData<string>(9)?.ToUpper();
            bullet.SizeValue = size switch
            {
                "N" => BulletSize.Normal,
                "L" => BulletSize.Large,
                _ => throw new NotImplementedException($"SizeValue = {size}"),
            };

            var type = args.GetData<string>(10)?.ToUpper();
            bullet.TypeValue = type switch
            {
                "CIR" => BulletType.Circle,
                "NDL" => BulletType.Needle,
                "SQR" => BulletType.Square,
                _ => throw new NotImplementedException($"TypeValue = {type}"),
            };

            bullet.RandomOffsetRange = args.GetData<int>(11);

            return bullet;
        }
    }
}
