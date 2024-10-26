using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class BellDrawingTarget : BulletPalleteReferencableBatchDrawTargetBase<Bell>
    {
        private readonly Texture texture;
        private readonly Vector2 sizeNormal;
        private readonly Vector2 sizeLarge;

        public BellDrawingTarget()
        {
            texture = ResourceUtils.OpenReadTextureFromFile(@".\Resources\editor\bell.png");

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("bell", out var size, out _))
                size = new Vector2(40, 40);
            sizeNormal = size;
            sizeLarge = sizeNormal * 1.4f;

            normalDrawList[texture] = new();
            selectedDrawList[texture] = new();
        }

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BEL" };
        public override int DefaultRenderOrder => 1000;

        public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bell obj, Vector2 pos, float rotate)
        {
            var size = obj.ReferenceBulletPallete?.SizeValue is BulletSize.Large ? sizeLarge : sizeNormal;
            var offsetPos = pos;

            normalDrawList[texture].Add((size, offsetPos, 0));
            if (obj.IsSelected)
                selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0));
            drawStrList.Add((offsetPos, obj));
            target.RegisterSelectableObject(obj, offsetPos, size);
        }

        public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bell obj, Vector2 pos, float rotate)
        {
            var size = obj.ReferenceBulletPallete?.SizeValue is BulletSize.Large ? sizeLarge : sizeNormal;
            var offsetPos = pos;

            normalDrawList[texture].Add((size, offsetPos, 0));
            if (obj.IsSelected)
                selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0));
        }
    }
}
