using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Parser.Ogkr.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
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
            bpl.ShooterValue = dataStrArr.ElementAtOrDefault(2)?.ToUpperInvariant() switch
            {
                "UPS" => Shooter.TargetHead,
                "ENE" => Shooter.Enemy,
                "CEN" => Shooter.Center,
                _ => throw new NotImplementedException(),
            };
            bpl.PlaceOffset = dataIntArr.ElementAtOrDefault(3);
            bpl.TargetValue = dataStrArr.ElementAtOrDefault(4)?.ToUpperInvariant() switch
            {
                "PLR" => Target.Player,
                "FIX" => Target.FixField,
                _ => throw new NotImplementedException(),
            };
            bpl.Speed = dataFloatArr.ElementAtOrDefault(5);
            bpl.SizeValue = dataStrArr.ElementAtOrDefault(6)?.ToUpperInvariant() switch
            {
                "L" => BulletSize.Large,
                "N" or _ => BulletSize.Normal,
            };
            bpl.TypeValue = dataStrArr.ElementAtOrDefault(7)?.ToUpperInvariant() switch
            {
                "SQR" => BulletType.Square,
                "NDL" => BulletType.Needle,
                "CIR" or _ => BulletType.Circle,
            };
            bpl.RandomOffsetRange = dataIntArr.ElementAtOrDefault(8);

            // 游戏侧 BulletPalleteList 是 Dictionary<string, BulletPallete>：同 strID 的第二条会抛
            // ArgumentException（读谱崩溃）。编辑器这里会静默用新模板替换旧模板，所以只能在此记缺陷。
            if (fumen.BulletPalleteList.Any(x => string.Equals(x.StrID, bpl.StrID, StringComparison.Ordinal)))
                ReportBulletPalleteIdDuplicate(fumen, args, bpl.StrID);

            return bpl;
        }
    }
}
