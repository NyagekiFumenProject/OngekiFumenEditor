using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl
{
    [Export(typeof(ICommandParser))]
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

            return bullet;
        }
    }
}
