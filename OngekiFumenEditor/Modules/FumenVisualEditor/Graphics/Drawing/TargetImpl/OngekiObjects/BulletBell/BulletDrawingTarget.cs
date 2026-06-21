using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class BulletDrawingTarget : ProjectileBatchDrawTargetBase<Bullet>
    {
        private readonly struct SpriteInfo
        {
            public readonly IImage Texture;
            public readonly Vector2 Size;
            public readonly Vector2 OriginOffset;

            public SpriteInfo(IImage texture, Vector2 size, Vector2 originOffset)
            {
                Texture = texture;
                Size = size;
                OriginOffset = originOffset;
            }
        }

        // [BulletDamageType, BulletType, BulletSize]
        private SpriteInfo[,,] spriteInfoTable;

        public override void Initialize(IRenderManagerImpl impl)
        {
            base.Initialize(impl);

            var damageTypeCount = Enum.GetValues<BulletDamageType>().Length;
            var bulletTypeCount = Enum.GetValues<BulletType>().Length;
            var bulletSizeCount = Enum.GetValues<BulletSize>().Length;
            var table = new SpriteInfo[damageTypeCount, bulletTypeCount, bulletSizeCount];

            void SetTexture(BulletDamageType k1, BulletType k2, string rPath, string key, Vector2 size, Vector2 origOffset, Vector2 sizeLarge, Vector2 origOffsetLarge)
            {
                var tex = ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\" + rPath);
                normalDrawList[tex] = new List<(Vector2, Vector2, float, Vector4)>();
                selectedDrawList[tex] = new List<(Vector2, Vector2, float, Vector4)>();

                if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile(key + "Normal", out var cfgSize, out var cfgOrigin))
                {
                    cfgSize = size;
                    cfgOrigin = origOffset;
                }
                table[(int)k1, (int)k2, (int)BulletSize.Normal] = new SpriteInfo(tex, cfgSize, cfgOrigin);

                if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile(key + "Large", out cfgSize, out cfgOrigin))
                {
                    cfgSize = sizeLarge;
                    cfgOrigin = origOffsetLarge;
                }
                table[(int)k1, (int)k2, (int)BulletSize.Large] = new SpriteInfo(tex, cfgSize, cfgOrigin);
            }

            var size = new Vector2(40, 40);
            var origOffset = new Vector2(0, 0);
            var sizeLarge = size * 1.4f;
            var origOffsetLarge = origOffset * 1.4f;
            SetTexture(BulletDamageType.Normal, BulletType.Circle, "bulletCircleNormal.png", "bulletCircle", size, origOffset, sizeLarge, origOffsetLarge);
            SetTexture(BulletDamageType.Hard, BulletType.Circle, "bulletCircleHard.png", "bulletCircle", size, origOffset, sizeLarge, origOffsetLarge);
            SetTexture(BulletDamageType.Danger, BulletType.Circle, "bulletCircleDanger.png", "bulletCircle", size, origOffset, sizeLarge, origOffsetLarge);

            size = new(30, 80);
            origOffset = new Vector2(0, 35);
            sizeLarge = size * 1.4f;
            origOffsetLarge = origOffset * 1.4f;
            SetTexture(BulletDamageType.Normal, BulletType.Needle, "bulletNeedleNormal.png", "bulletNeedle", size, origOffset, sizeLarge, origOffsetLarge);
            SetTexture(BulletDamageType.Hard, BulletType.Needle, "bulletNeedleHard.png", "bulletNeedle", size, origOffset, sizeLarge, origOffsetLarge);
            SetTexture(BulletDamageType.Danger, BulletType.Needle, "bulletNeedleDanger.png", "bulletNeedle", size, origOffset, sizeLarge, origOffsetLarge);

            size = new(30, 80);
            origOffset = new Vector2(0, 35);
            sizeLarge = size * 1.4f;
            origOffsetLarge = origOffset * 1.4f;
            SetTexture(BulletDamageType.Normal, BulletType.Square, "bulletSquareNormal.png", "bulletSquare", size, origOffset, sizeLarge, origOffsetLarge);
            SetTexture(BulletDamageType.Hard, BulletType.Square, "bulletSquareHard.png", "bulletSquare", size, origOffset, sizeLarge, origOffsetLarge);
            SetTexture(BulletDamageType.Danger, BulletType.Square, "bulletSquareDanger.png", "bulletSquare", size, origOffset, sizeLarge, origOffsetLarge);

            spriteInfoTable = table;
        }

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BLT" };
        public override int DefaultRenderOrder => 1500;

        public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bullet obj, Vector2 pos, float rotate, DrawBuffer buffer)
        {
            ref readonly var info = ref spriteInfoTable[(int)obj.BulletDamageTypeValue, (int)obj.TypeValue, (int)obj.SizeValue];

            var offsetPos = pos + info.OriginOffset;
            buffer.Normal[info.Texture].Add((info.Size, offsetPos, 0, Vector4.One));
            if (obj.IsSelected)
                buffer.Selected[info.Texture].Add((info.Size * 1.3f, offsetPos, 0, Vector4.One));
            if (obj.ReferenceBulletPallete is { } pallete)
                buffer.StrList.Add((offsetPos, pallete.StrID));
            target.RegisterSelectableObject(obj, offsetPos, info.Size);
        }

        public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bullet obj, Vector2 pos, float rotate, DrawBuffer buffer)
        {
            ref readonly var info = ref spriteInfoTable[(int)obj.BulletDamageTypeValue, (int)obj.TypeValue, (int)obj.SizeValue];

            var offsetPos = pos + info.OriginOffset;
            buffer.Normal[info.Texture].Add((info.Size, offsetPos, rotate, Vector4.One));
            if (obj.IsSelected)
                buffer.Selected[info.Texture].Add((info.Size * 1.3f, offsetPos, rotate, Vector4.One));
        }
    }
}
