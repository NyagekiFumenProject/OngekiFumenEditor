using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Kernel.Graphics.Base;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Drawing;
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
		private IDictionary<Texture, Vector2> spritesSize;
		private IDictionary<Texture, Vector2> spritesOriginOffset;
		private IDictionary<BulletDamageType, Dictionary<BulletType, Texture>> spritesMap;

		public BulletDrawingTarget()
		{
			Texture LoadTex(string rPath)
			{
				var info = System.Windows.Application.GetResourceStream(new Uri(@"Modules\FumenVisualEditor\Views\OngekiObjects\" + rPath, UriKind.Relative));
				using var bitmap = Image.FromStream(info.Stream) as Bitmap;
				return new Texture(bitmap);
			}

			var _spritesOriginOffset = new Dictionary<Texture, Vector2>();
			var _spritesSize = new Dictionary<Texture, Vector2>();
			var _spritesMap = new Dictionary<BulletDamageType, Dictionary<BulletType, Texture>>();

			void SetTexture(BulletDamageType k1, BulletType k2, string rPath, Vector2 size, Vector2 origOffset)
			{
				if (!_spritesMap.TryGetValue(k1, out var dic))
				{
					dic = new Dictionary<BulletType, Texture>();
					_spritesMap[k1] = dic;
				}

				var tex = LoadTex(rPath);
				dic[k2] = tex;
				normalDrawList[tex] = new();
				selectedDrawList[tex] = new();

				_spritesSize[tex] = size;
				_spritesOriginOffset[tex] = origOffset;
			}

			var size = new Vector2(40, 40);
			var origOffset = new Vector2(0, 0);
			SetTexture(BulletDamageType.Normal, BulletType.Circle, "nt_mine_red.png", size, origOffset);
			SetTexture(BulletDamageType.Hard, BulletType.Circle, "nt_mine_pur.png", size, origOffset);
			SetTexture(BulletDamageType.Danger, BulletType.Circle, "nt_mine_blk.png", size, origOffset);

			size = new(30, 80);
			origOffset = new Vector2(0, 35);
			SetTexture(BulletDamageType.Normal, BulletType.Needle, "tri_bullet0.png", size, origOffset);
			SetTexture(BulletDamageType.Hard, BulletType.Needle, "tri_bullet1.png", size, origOffset);
			SetTexture(BulletDamageType.Danger, BulletType.Needle, "tri_bullet2.png", size, origOffset);

			size = new(30, 80);
			origOffset = new Vector2(0, 35);
			SetTexture(BulletDamageType.Normal, BulletType.Square, "sqrt_bullet0.png", size, origOffset);
			SetTexture(BulletDamageType.Hard, BulletType.Square, "sqrt_bullet1.png", size, origOffset);
			SetTexture(BulletDamageType.Danger, BulletType.Square, "sqrt_bullet2.png", size, origOffset);

			spritesMap = _spritesMap.ToImmutableDictionary();
			spritesSize = _spritesSize.ToImmutableDictionary();
			spritesOriginOffset = _spritesOriginOffset.ToImmutableDictionary();
		}

		public override IEnumerable<string> DrawTargetID { get; } = new[] { "BLT" };
		public override int DefaultRenderOrder => 1500;

		public override void DrawVisibleObject_DesignMode(IFumenEditorDrawingContext target, Bullet obj, Vector2 pos, float rotate)
		{
			var damageType = obj.BulletDamageTypeValue;
			var bulletType = obj.ReferenceBulletPallete.TypeValue;

			var texture = spritesMap[damageType][bulletType];
			var size = spritesSize[texture];
			var origOffset = spritesOriginOffset[texture];

			var offsetPos = pos + origOffset;
			normalDrawList[texture].Add((size, offsetPos, 0));
			if (obj.IsSelected)
				selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0));
			drawStrList.Add((offsetPos, obj));
			target.RegisterSelectableObject(obj, offsetPos, size);
		}

		public override void DrawVisibleObject_PreviewMode(IFumenEditorDrawingContext target, Bullet obj, Vector2 pos, float rotate)
		{
			var damageType = obj.BulletDamageTypeValue;
			var bulletType = obj.ReferenceBulletPallete.TypeValue;

			var texture = spritesMap[damageType][bulletType];
			var size = spritesSize[texture];
			var origOffset = spritesOriginOffset[texture];

			var offsetPos = pos + origOffset;
			normalDrawList[texture].Add((size, offsetPos, rotate));
			if (obj.IsSelected)
				selectedDrawList[texture].Add((size * 1.3f, offsetPos, rotate));
		}
	}
}
