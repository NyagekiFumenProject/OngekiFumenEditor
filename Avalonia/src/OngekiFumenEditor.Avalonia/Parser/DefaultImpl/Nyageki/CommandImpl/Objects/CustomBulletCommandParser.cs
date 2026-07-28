using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
    [RegisterSingleton<INyagekiCommandParser>]
    public class CustomBulletCommandParser : INyagekiCommandParser
    {
        public string CommandName => "CustomBullet";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            //Bullet:{bullet.ReferenceBulletPallete?.StrID}:X[{bullet.XGrid.Unit},{bullet.XGrid.Grid}],T[{bullet.TGrid.Unit},{bullet.TGrid.Grid}],D[{bullet.BulletDamageTypeValue}]
            var bullet = new Bullet();
            var data = seg[1].Split(":");

            bullet.ReferenceBulletPallete = BulletPallete.DummyCustomPallete;

            using var d = data[0].GetValuesMapWithDisposable(out var map);
            bullet.TGrid = map["T"].ParseToTGrid();
            bullet.XGrid = map["X"].ParseToXGrid();
            bullet.BulletDamageTypeValue = Enum.Parse<BulletDamageType>(map["D"]);

            bullet.Speed = map["Speed"].ToFloatOrThrow();
            bullet.PlaceOffset = map["PlaceOffset"].ToIntOrThrow();
            bullet.RandomOffsetRange = map["RandomOffsetRange"].ToIntOrThrow();
            bullet.TypeValue = map["TypeValue"].ToEnumOrThrow<BulletType>();
            bullet.SizeValue = map["SizeValue"].ToEnumOrThrow<BulletSize>();
            bullet.ShooterValue = map["ShooterValue"].ToEnumOrThrow<Shooter>();
            bullet.TargetValue = map["TargetValue"].ToEnumOrThrow<Target>();

            fumen.AddObject(bullet);
        }
    }
}


