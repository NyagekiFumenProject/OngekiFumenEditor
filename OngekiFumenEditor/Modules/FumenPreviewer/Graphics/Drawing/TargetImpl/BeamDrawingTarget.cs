using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Beam;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.PrimitiveValue;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl
{
    [Export(typeof(IDrawingTarget))]
    public class BeamDrawingTarget : CommonSpriteDrawTargetBase<BeamStart>
    {
        private Texture texture;

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BMS" };

        public BeamDrawingTarget() : base()
        {
            var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\nt_linehold_pur.png", UriKind.Relative));
            using var bitmap = Image.FromStream(info.Stream) as Bitmap;
            texture = new Texture(bitmap);
        }

        public override void Draw(BeamStart obj, OngekiFumen fumen)
        {
            /*
            (var fromBeam, var fromY) = obj.Children.AsEnumerable<BeamBase>()
                .Append(obj)
                .Select(x => (x, TGridCalculator.ConvertTGridToY(x.TGrid, fumen.BpmList, 240)))
                .LastOrDefault(x => x.Item2 <= Previewer.CurrentPlayTime);

            if (fromBeam is null)
                return;
            var toBeam = obj.Children.AsEnumerable<BeamBase>()
                .Append(obj).FindNextOrDefault(fromBeam);
            if (toBeam is null)
                return;
            var fromX = XGridCalculator.ConvertXGridToX(fromBeam.XGrid, 30, Previewer.ViewWidth, 1);
            var toX = XGridCalculator.ConvertXGridToX(toBeam.XGrid, 30, Previewer.ViewWidth, 1);
            var toY = TGridCalculator.ConvertTGridToY(toBeam.TGrid, fumen.BpmList, 240);

            */
            //Draw(texture, size, pos, 0);
        }

        public override void Dispose()
        {
            base.Dispose();
            texture?.Dispose();
            texture = null;
        }
    }
}
