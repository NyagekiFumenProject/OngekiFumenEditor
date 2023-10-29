using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static OngekiFumenEditor.Base.OngekiObjects.Bullet;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	public class BulletBellDrawingTarget : CommonBatchDrawTargetBase<OngekiMovableObjectBase>, IDisposable
	{
		public override int DefaultRenderOrder => 1500;

		private const BulletDamageType BellDamageType = (BulletDamageType)(-1);
		private const BulletType BellBulletType = (BulletType)(-1);
		private readonly int parallelCountLimit;
		private SoflanList nonSoflanList = new(new[] { new Soflan() { TGrid = TGrid.Zero, Speed = 1 } });

		IDictionary<BulletDamageType, Dictionary<BulletType, Texture>> spritesMap;
		private ParallelOptions parallelOptions;
		IDictionary<Texture, Vector2> spritesSize;
		IDictionary<Texture, Vector2> spritesOriginOffset;

		Dictionary<Texture, ConcurrentBag<(Vector2, Vector2, float)>> normalDrawList = new();
		Dictionary<Texture, ConcurrentBag<(Vector2, Vector2, float)>> selectedDrawList = new();

		private List<(Vector2 pos, IBulletPalleteReferencable obj)> drawStrList = new();
		private IStringDrawing stringDrawing;
		private IHighlightBatchTextureDrawing highlightDrawing;
		private IBatchTextureDrawing batchTextureDrawing;

		public override IEnumerable<string> DrawTargetID { get; } = new[] { Bullet.CommandName, Bell.CommandName };

		public BulletBellDrawingTarget() : base()
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

			//特殊处理
			SetTexture(BellDamageType, BellBulletType, "bell.png", size, origOffset);

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


			stringDrawing = IoC.Get<IStringDrawing>();
			batchTextureDrawing = IoC.Get<IBatchTextureDrawing>();
			highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();

			spritesOriginOffset = _spritesOriginOffset.ToImmutableDictionary();
			spritesSize = _spritesSize.ToImmutableDictionary();
			spritesMap = _spritesMap.ToImmutableDictionary();

			parallelOptions = new ParallelOptions()
			{
				MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 2),
			};

			Log.LogDebug($"BulletDrawingTarget.MaxDegreeOfParallelism = {parallelOptions.MaxDegreeOfParallelism}");

			parallelCountLimit = Properties.EditorGlobalSetting.Default.ParallelCountLimit;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float CalculateBulletMsecTime(IFumenEditorDrawingContext target, IBulletPalleteReferencable obj, float userSpeed = 2.35f)
		{
			//const float fat = 3.95f;
			//var time =  32.5f * fat / (Math.Max(4.7f, 0.2f * userSpeed) * (/*obj.ReferenceBulletPallete?.Speed ??*/ 1f)) * 16.666666f;

			var time = target.ViewHeight / (obj.ReferenceBulletPallete?.Speed ?? 1f);
			return (float)time;
		}

		private void DrawEditor(IFumenEditorDrawingContext target, OngekiMovableObjectBase obj)
		{
			var toX = XGridCalculator.ConvertXGridToX(obj.XGrid, target.Editor);
			var toTime = target.ConvertToY(obj.TGrid);
			var bulletPallateRefObj = obj as IBulletPalleteReferencable;

			var pos = new Vector2((float)toX, (float)toTime);

			var isBell = obj is Bell;
			var damageType = isBell ? BellDamageType : ((Bullet)obj).BulletDamageTypeValue;
			var bulletType = isBell ? BellBulletType : bulletPallateRefObj.ReferenceBulletPallete.TypeValue;

			var texture = spritesMap[damageType][bulletType];
			var size = spritesSize[texture];
			var origOffset = spritesOriginOffset[texture];

			var offsetPos = pos + origOffset;
			normalDrawList[texture].Add((size, offsetPos, 0));
			if (obj.IsSelected)
				selectedDrawList[texture].Add((size * 1.3f, offsetPos, 0));
			drawStrList.Add((offsetPos, bulletPallateRefObj));
			target.RegisterSelectableObject(obj, offsetPos, size);
		}

		private void DrawPallateStr(IDrawingContext target)
		{
			foreach ((var pos, var obj) in drawStrList)
			{
				if (obj.ReferenceBulletPallete is null)
					return;
				stringDrawing.Draw($"{obj.ReferenceBulletPallete.StrID}", new(pos.X, pos.Y + 5), Vector2.One, 16, 0, Vector4.One, new(0.5f, 0.5f), default, target, default, out _);
			}
		}

		public void Dispose()
		{
			spritesMap.SelectMany(x => x.Value.Values).ForEach(x => x.Dispose());
			spritesMap.Clear();
			ClearDrawList();
		}

		private void ClearDrawList()
		{
			foreach (var l in normalDrawList.Values)
				l.Clear();
			foreach (var l in selectedDrawList.Values)
				l.Clear();
			drawStrList.Clear();
		}

		ConcurrentDictionary<double, double> yCacheMap = new();
		ConcurrentDictionary<double, double> nonSoflanYCacheMap = new();
		ConcurrentDictionary<double, double> xCacheMap = new();

		private void DrawPreview(IEnumerable<OngekiMovableObjectBase> objs)
		{
			var currentTGrid = TGridCalculator.ConvertAudioTimeToTGrid(target.CurrentPlayTime, target.Editor);
			var baseY = Math.Min(target.Rect.MinY, target.Rect.MaxY) + target.Editor.Setting.JudgeLineOffsetY;

			/*
            yCacheMap.Clear();
            xCacheMap.Clear();
            nonSoflanYCacheMap.Clear();
            */

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			double convertToYNonSoflan(TGrid tgrid)
			{
				/*var key = tgrid.TotalUnit;
                if (nonSoflanYCacheMap.TryGetValue(key, out var r))
                    return r;
                return nonSoflanYCacheMap[key] = */
				return TGridCalculator.ConvertTGridToY_DesignMode(
					tgrid,
					nonSoflanList,
					target.Editor.Fumen.BpmList,
					target.Editor.Setting.VerticalDisplayScale,
					target.Editor.Setting.TGridUnitLength);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			double convertToX(double xgrid)
			{
				/*if (xCacheMap.TryGetValue(xgrid, out var r))
                    return r;
                return xCacheMap[xgrid] =*/
				return XGridCalculator.ConvertXGridToX(xgrid, target.Editor);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			double convertToY(TGrid tgrid)
			{
				/*var key = tgrid.TotalUnit;
                if (yCacheMap.TryGetValue(key, out var r))
                    return r;
                return yCacheMap[key] =*/
				return target.ConvertToY(tgrid);
			}

			void _Draw(OngekiMovableObjectBase obj)
			{
				var bulletPallateRefObj = obj as IBulletPalleteReferencable;
				/*
                --------------------------- toTime 
                        \
                         \
                          \
                           \
                            \
                             O      <- currentTime
                              bell
                               \
                                \
                                 \
                                  \
                                   \
                ---------------------------- fromTime = toTime - appearOffsetTime
                 */

				//计算向量化的物件运动时间
				var appearOffsetTime = CalculateBulletMsecTime(target, bulletPallateRefObj);

				var toTime = 0d;
				var currentTime = 0d;

				if (!(bulletPallateRefObj.ReferenceBulletPallete?.IsEnableSoflan ?? true))
				{
					toTime = convertToYNonSoflan(obj.TGrid);
					currentTime = convertToYNonSoflan(currentTGrid);
				}
				else
				{
					toTime = convertToY(obj.TGrid);
					currentTime = convertToY(currentTGrid);
				}

				var fromTime = toTime - appearOffsetTime;
				var precent = (currentTime - fromTime) / appearOffsetTime;
				var timeY = baseY + target.Rect.Height * (1 - precent);

				if (!(target.Rect.MinY <= timeY && timeY <= target.Rect.MaxY))
					return;

				var fromX = convertToX(bulletPallateRefObj.ReferenceBulletPallete?.CalculateFromXGridTotalUnit(bulletPallateRefObj, target.Editor.Fumen) ?? obj.XGrid.TotalUnit);
				var toX = convertToX(bulletPallateRefObj.ReferenceBulletPallete?.CalculateToXGridTotalUnit(bulletPallateRefObj, target.Editor.Fumen) ?? obj.XGrid.TotalUnit);
				var timeX = MathUtils.CalculateXFromTwoPointFormFormula(currentTime, fromX, fromTime, toX, toTime);

				if (!(target.Rect.MinX <= timeX && timeX <= target.Rect.MaxX))
					return;

				var rotate = (float)Math.Atan((toX - fromX) / (toTime - fromTime));

				var pos = new Vector2((float)timeX, (float)timeY);

				var isBell = obj is Bell;
				var damageType = isBell ? BellDamageType : ((Bullet)obj).BulletDamageTypeValue;
				var bulletType = isBell ? BellBulletType : bulletPallateRefObj.ReferenceBulletPallete.TypeValue;

				var texture = spritesMap[damageType][bulletType];
				var size = spritesSize[texture];
				var origOffset = spritesOriginOffset[texture];

				var offsetPos = pos + origOffset;
				normalDrawList[texture].Add((size, offsetPos, rotate));
				if (obj.IsSelected)
					selectedDrawList[texture].Add((size * 1.3f, offsetPos, rotate));
			}

			/*
             存在spd < 1或者soflan影响的子弹/bell物件。因此无法简单的使用二分法快速枚举筛选物件
             使用并行计算，将所有bell/bullet全部判断，当然判断的结果也能直接拿来做计算
             //todo 还能优化
             */
			if (objs.Count() < parallelCountLimit)
			{
				foreach (var obj in objs)
					_Draw(obj);
			}
			else
			{
				//todo 使用并行会有闪烁问题，原因不明
				Parallel.ForEach(objs, parallelOptions, _Draw);
			}
		}

		public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<OngekiMovableObjectBase> objs)
		{
			if (target.Editor.IsDesignMode)
			{
				foreach (var obj in objs)
					DrawEditor(target, obj);
			}
			else
			{
				DrawPreview(objs);
			}

			foreach (var item in selectedDrawList)
				highlightDrawing.Draw(target, item.Key, item.Value.OrderBy(x => x.Item2.Y));

			foreach (var item in normalDrawList)
				batchTextureDrawing.Draw(target, item.Key, item.Value.OrderBy(x => x.Item2.Y));

			if (target.Editor.IsDesignMode)
				DrawPallateStr(target);
			ClearDrawList();
		}
	}
}
