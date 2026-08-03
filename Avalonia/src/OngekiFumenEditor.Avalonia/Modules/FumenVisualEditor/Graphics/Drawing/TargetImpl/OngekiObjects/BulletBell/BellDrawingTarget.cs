using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using Injectio.Attributes;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
    [RegisterSingleton<IFumenEditorDrawingTarget>]
    public class BellDrawingTarget : ProjectileBatchDrawTargetBase<Bell>
    {
        private IImage texture;
        private Vector2 sizeNormal;
        private Vector2 sizeLarge;

        public override void Initialize(IRenderManagerImpl impl)
        {
            base.Initialize(impl);

            texture = ResourceUtils.OpenReadTextureFromResource(impl, "editor/bell.png");

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("bell", out var size, out _))
                size = new Vector2(40, 40);
            sizeNormal = size;
            sizeLarge = sizeNormal * 1.4f;

            normalDrawList[texture] = new();
            selectedDrawList[texture] = new();
        }

        public override IEnumerable<string> DrawTargetID { get; } = ["BEL"];
        public override int DefaultRenderOrder => 1000;

        public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bell obj, Vector2 pos, float rotate)
        {
            var size = obj.SizeValue is BulletSize.Large ? sizeLarge : sizeNormal;
            var offsetPos = pos;

            normalDrawList[texture].Add((size, offsetPos, 0, Vector4.One));
            if (obj.IsSelected)
                selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0, Vector4.One));
            if (obj.ReferenceBulletPallete is { } pallete && pallete != BulletPallete.DummyCustomPallete)
                drawStrList.Add((offsetPos, pallete.StrID));
            target.RegisterSelectableObject(obj, offsetPos, size);
        }

        public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bell obj, Vector2 pos, float rotate)
        {
            var size = obj.SizeValue is BulletSize.Large ? sizeLarge : sizeNormal;
            var offsetPos = pos;

            normalDrawList[texture].Add((size, offsetPos, 0, Vector4.One));
            if (obj.IsSelected)
                selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0, Vector4.One));
        }
    }
}


