using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Utils;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects
{
    [RegisterSingleton<INyagekiCommandParser>]
    public class CustomBellCommandParser : INyagekiCommandParser
    {
        public string CommandName => "CustomBell";

        public void ParseAndApply(OngekiFumen fumen, string[] seg)
        {
            //$"Bell:{bell.ReferenceBulletPallete?.StrID}:X[{bell.XGrid.Unit},{bell.XGrid.Grid}],T[{bell.TGrid.Unit},{bell.TGrid.Grid}]"
            var bell = new Bell();
            var data = seg[1].Split(":");

            bell.ReferenceBulletPallete = BulletPallete.DummyCustomPallete;

            using var d = data[0].GetValuesMapWithDisposable(out var map);
            bell.TGrid = map["T"].ParseToTGrid();
            bell.XGrid = map["X"].ParseToXGrid();

            bell.Speed = map["Speed"].ToFloatOrThrow();
            bell.PlaceOffset = map["PlaceOffset"].ToIntOrThrow();
            bell.RandomOffsetRange = map["RandomOffsetRange"].ToIntOrThrow();
            bell.SizeValue = map["SizeValue"].ToEnumOrThrow<BulletSize>();
            bell.ShooterValue = map["ShooterValue"].ToEnumOrThrow<Shooter>();
            bell.TargetValue = map["TargetValue"].ToEnumOrThrow<Target>();

            fumen.AddObject(bell);
        }
    }
}


