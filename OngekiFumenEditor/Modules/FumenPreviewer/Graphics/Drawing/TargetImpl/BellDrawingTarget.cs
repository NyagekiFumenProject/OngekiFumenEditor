using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class BellDrawingTarget : CommonSpriteDrawTargetBase<Bell>
    {
        public override IEnumerable<string> DrawTargetID { get; } = new[] { Bell.CommandName };

        public BellDrawingTarget() : base(new Texture(Properties.Resources.bell))
        {

        }

        protected override Vector GetObjectPosition(Bell obj, OngekiFumen fumen)
        {
            var y = TGridCalculator.ConvertTGridToY(obj.TGrid, fumen.BpmList, 240);
            var x = XGridCalculator.ConvertXGridToX(obj.XGrid, 30, Previewer.ViewWidth, 1);

            return new((float)x, (float)y);
        }
    }
}
