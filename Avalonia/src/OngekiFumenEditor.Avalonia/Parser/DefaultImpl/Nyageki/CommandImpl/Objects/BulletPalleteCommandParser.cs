using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using System;
using static OngekiFumenEditor.Avalonia.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
    [RegisterSingleton<INyagekiCommandParser>]
    public class BulletPalleteCommandParser : INyagekiCommandParser
    {
        public string CommandName => "BulletPallete";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            var bpl = new BulletPallete();

            var data = seg[1].Split(":");
            using var d = data[1].GetValuesMapWithDisposable(out var map);

            bpl.StrID = data[0].Trim();
            bpl.ShooterValue = Enum.Parse<Shooter>(map["Shooter"]);
            bpl.TargetValue = Enum.Parse<Target>(map["Target"]);
            bpl.SizeValue = Enum.Parse<BulletSize>(map["Size"]);
            bpl.TypeValue = Enum.Parse<BulletType>(map["Type"]);
            bpl.Speed = float.Parse(map["Speed"]);
            bpl.PlaceOffset = int.Parse(map["PlaceOffset"]);

            //兼容老铺�?
            if (map.TryGetValue("RandomOffsetRange", out var r))
                bpl.RandomOffsetRange = int.Parse(r);

            fumen.AddObject(bpl);
        }
    }
}



