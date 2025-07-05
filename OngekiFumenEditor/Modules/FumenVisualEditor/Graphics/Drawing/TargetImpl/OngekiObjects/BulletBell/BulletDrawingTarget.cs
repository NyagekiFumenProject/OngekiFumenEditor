using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
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
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects.BulletBell
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public class BulletDrawingTarget : BulletPalleteReferencableBatchDrawTargetBase<Bullet>
    {
        private IDictionary<IImage, Vector2> spritesSize;
        private IDictionary<IImage, Vector2> spritesOriginOffset;
        private IDictionary<IImage, Vector2> spritesSizeLarge;
        private IDictionary<IImage, Vector2> spritesOriginOffsetLarge;
        private IDictionary<BulletDamageType, Dictionary<BulletType, IImage>> spritesMap;

        public override void Initialize(IRenderManagerImpl impl)
        {
            base.Initialize(impl);

            var _spritesOriginOffset = new Dictionary<IImage, Vector2>();
            var _spritesSize = new Dictionary<IImage, Vector2>();
            var _spritesOriginOffsetLarge = new Dictionary<IImage, Vector2>();
            var _spritesSizeLarge = new Dictionary<IImage, Vector2>();
            var _spritesMap = new Dictionary<BulletDamageType, Dictionary<BulletType, IImage>>();

            void SetTexture(BulletDamageType k1, BulletType k2, string rPath, string key, Vector2 size, Vector2 origOffset, Vector2 sizeLarge, Vector2 origOffsetLarge)
            {
                if (!_spritesMap.TryGetValue(k1, out var dic))
                {
                    dic = new Dictionary<BulletType, IImage>();
                    _spritesMap[k1] = dic;
                }

                var tex = ResourceUtils.OpenReadTextureFromFile(impl, @".\Resources\editor\" + rPath);
                dic[k2] = tex;
                normalDrawList[tex] = new();
                selectedDrawList[tex] = new();

                if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile(key + "Normal", out var cfgSize, out var cfgOrigin))
                {
                    cfgSize = size;
                    cfgOrigin = origOffset;
                }
                _spritesSize[tex] = cfgSize;
                _spritesOriginOffset[tex] = cfgOrigin;

                if (!ResourceUtils.OpenReadTextureSizeAnchorByConfigFile(key + "Large", out cfgSize, out cfgOrigin))
                {
                    cfgSize = sizeLarge;
                    cfgOrigin = origOffsetLarge;
                }
                _spritesSizeLarge[tex] = cfgSize;
                _spritesOriginOffsetLarge[tex] = cfgOrigin;
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

            spritesMap = _spritesMap.ToImmutableDictionary();
            spritesSize = _spritesSize.ToImmutableDictionary();
            spritesOriginOffset = _spritesOriginOffset.ToImmutableDictionary();
            spritesSizeLarge = _spritesSizeLarge.ToImmutableDictionary();
            spritesOriginOffsetLarge = _spritesOriginOffsetLarge.ToImmutableDictionary();
        }

        public override IEnumerable<string> DrawTargetID { get; } = new[] { "BLT" };
        public override int DefaultRenderOrder => 1500;

        public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bullet obj, Vector2 pos, float rotate)
        {
            if (obj.ReferenceBulletPallete is null)
            {
                Log.LogWarn($"Bullet {obj.Id} has null reference bullet pallete");
                return;
            }
            var damageType = obj.BulletDamageTypeValue;
            var bulletType = obj.ReferenceBulletPallete.TypeValue;

            var texture = spritesMap[damageType][bulletType];

            var isLarge = obj.ReferenceBulletPallete.SizeValue == BulletSize.Large;
            var size = (isLarge ? spritesSizeLarge : spritesSize)[texture];
            var origOffset = (isLarge ? spritesOriginOffsetLarge : spritesOriginOffset)[texture];

            var offsetPos = pos + origOffset;
            normalDrawList[texture].Add((size, offsetPos, 0, Vector4.One));
            if (obj.IsSelected)
                selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0, Vector4.One));
            drawStrList.Add((offsetPos, obj));
            target.RegisterSelectableObject(obj, offsetPos, size);
        }

        public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bullet obj, Vector2 pos, float rotate)
        {
            var damageType = obj.BulletDamageTypeValue;
            var bulletType = obj.ReferenceBulletPallete.TypeValue;

            var texture = spritesMap[damageType][bulletType];

            var isLarge = obj.ReferenceBulletPallete.SizeValue == BulletSize.Large;
            var size = (isLarge ? spritesSizeLarge : spritesSize)[texture];
            var origOffset = (isLarge ? spritesOriginOffsetLarge : spritesOriginOffset)[texture];

            var offsetPos = pos + origOffset;
            normalDrawList[texture].Add((size, offsetPos, rotate, Vector4.One));
            if (obj.IsSelected)
                selectedDrawList[texture].Add((size * 1.3f, offsetPos, rotate, Vector4.One));
        }
    }
}
