using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.Lane
{
    [Export(typeof(IDrawingTarget))]
    internal class NormalLaneEditorObjectDrawingTarget : CommonLaneEditorObjectDrawingTarget
    {
        public override Texture StartEditorTexture { get; }
        public override Texture NextEditorTexture { get; }
        public override Texture EndEditorTexture { get; }

        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "LLS","LCS","LRS"
        };

        public NormalLaneEditorObjectDrawingTarget() : base()
        {
            Texture LoadTex(string rPath)
            {
                var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\" + rPath, UriKind.Relative));
                using var bitmap = Image.FromStream(info.Stream) as Bitmap;
                return new Texture(bitmap);
            }

            StartEditorTexture = LoadTex("NS.png");
            NextEditorTexture = LoadTex("NN.png");
            EndEditorTexture = LoadTex("NE.png");
        }
    }

    [Export(typeof(IDrawingTarget))]
    internal class WallLaneEditorObjectDrawingTarget : CommonLaneEditorObjectDrawingTarget
    {
        public override Texture StartEditorTexture { get; }
        public override Texture NextEditorTexture { get; }
        public override Texture EndEditorTexture { get; }

        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "WLS","WRS"
        };

        public WallLaneEditorObjectDrawingTarget() : base()
        {
            Texture LoadTex(string rPath)
            {
                var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\" + rPath, UriKind.Relative));
                using var bitmap = Image.FromStream(info.Stream) as Bitmap;
                return new Texture(bitmap);
            }

            StartEditorTexture = LoadTex("WS.png");
            NextEditorTexture = LoadTex("WN.png");
            EndEditorTexture = LoadTex("WE.png");
        }
    }

    [Export(typeof(IDrawingTarget))]
    internal class BeamEditorObjectDrawingTarget : CommonLaneEditorObjectDrawingTarget
    {
        public override Texture StartEditorTexture { get; }
        public override Texture NextEditorTexture { get; }
        public override Texture EndEditorTexture { get; }

        public override IEnumerable<string> DrawTargetID { get; } = new[]
        {
            "BMS"
        };

        public BeamEditorObjectDrawingTarget() : base()
        {
            Texture LoadTex(string rPath)
            {
                var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\" + rPath, UriKind.Relative));
                using var bitmap = Image.FromStream(info.Stream) as Bitmap;
                return new Texture(bitmap);
            }

            StartEditorTexture = LoadTex("NS.png");
            NextEditorTexture = LoadTex("NN.png");
            EndEditorTexture = LoadTex("NE.png");
        }
    }
}
