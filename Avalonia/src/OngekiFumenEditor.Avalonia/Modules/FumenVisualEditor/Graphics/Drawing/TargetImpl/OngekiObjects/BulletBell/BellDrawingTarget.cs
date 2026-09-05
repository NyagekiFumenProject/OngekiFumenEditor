using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Avalonia.Kernel.Graphics;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
    [RegisterSingleton<IFumenEditorDrawingTarget>]
    public sealed class BellDrawingTarget : ProjectileBatchDrawTargetBase<Bell>
    {
        private IImage texture;
        private Vector2 sizeNormal;

        public override void Initialize(IRenderManagerImpl impl)
        {
            base.Initialize(impl);
            texture = ResourceUtils.OpenReadTextureFromResource(impl, "editor/bell.png");

            if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile("bell", out var size, out _))
                size = new Vector2(40, 40);
            sizeNormal = size;

            normalDrawList[texture] = new List<(Vector2, Vector2, float, Vector4)>();
            selectedDrawList[texture] = new List<(Vector2, Vector2, float, Vector4)>();
        }

        public override IEnumerable<string> DrawTargetID { get; } = ["BEL"];
        public override int DefaultRenderOrder => 1000;

        public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bell obj, Vector2 pos, float rotate, DrawBuffer buffer)
        {
            var size = sizeNormal;
            var offsetPos = pos;

            buffer.Normal[texture].Add((size, offsetPos, 0, Vector4.One));
            if (obj.IsSelected)
                buffer.Selected[texture].Add((size * 1.3f, offsetPos, 0, Vector4.One));
            if (obj.ReferenceBulletPallete is { } pallete)
                buffer.StrList.Add((offsetPos, pallete.StrID));
            target.RegisterSelectableObject(obj, offsetPos, size);
        }

        public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bell obj, Vector2 pos, float rotate, DrawBuffer buffer)
        {
            var size = sizeNormal;
            var offsetPos = pos;

            buffer.Normal[texture].Add((size, offsetPos, 0, Vector4.One));
            if (obj.IsSelected)
                buffer.Selected[texture].Add((size * 1.3f, offsetPos, 0, Vector4.One));
        }
    }
}
