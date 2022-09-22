using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.TargetImpl.Lane
{
    [Export(typeof(IDrawingTarget))]
    internal class WallLaneDrawTarget : LaneDrawingTargetBase
    {
        public WallLaneDrawTarget() : base()
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

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "WLS", "WRS" };

        public static Vector4 LeftWallColor { get; } = new(181 / 255.0f, 156 / 255.0f, 231 / 255.0f, 255 / 255.0f);
        public static Vector4 RightWallColor { get; } = new(231 / 255.0f, 149 / 255.0f, 178 / 255.0f, 255 / 255.0f);

        public override int LineWidth => 6;

        public override Vector4 GetLanePointColor(ConnectableObjectBase obj)
        {
            if (obj.IDShortName[1] == 'L')
                return LeftWallColor;
            return RightWallColor;
        }
    }
}
