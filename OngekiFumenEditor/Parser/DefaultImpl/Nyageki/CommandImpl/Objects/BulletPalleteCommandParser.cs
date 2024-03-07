using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using System;
using System.ComponentModel.Composition;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
    [Export(typeof(INyagekiCommandParser))]
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

            //兼容老铺面
            if (map.TryGetValue("RandomOffsetRange", out var r))
                bpl.RandomOffsetRange = int.Parse(r);

            fumen.AddObject(bpl);
        }
    }
}
