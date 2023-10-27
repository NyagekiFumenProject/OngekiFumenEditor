using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	[Export(typeof(TapDrawingTarget))]
	public class TapDrawingTarget : CommonBatchDrawTargetBase<Tap>, IDisposable
	{
		public override IEnumerable<string> DrawTargetID { get; } = new[] { "TAP", "CTP", "XTP" };

		public override int DefaultRenderOrder => 1200;

		private Texture redTexture;
		private Texture greenTexture;
		private Texture blueTexture;
		private Texture wallTexture;
		private Texture tapExTexture;
		private Texture wallExTexture;
		private Texture untagExTexture;

		private Vector2 tapSize = new Vector2(40, 16);
		private Vector2 exTapEffSize = new Vector2(40, 16);
		private Vector2 leftWallSize = new Vector2(40, 40);
		private Vector2 rightWallSize = new Vector2(-40, 40);

		private Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> normalList = new();
		private Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> exList = new();
		private Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> selectTapList = new();

		private IBatchTextureDrawing batchTextureDrawing;
		private IHighlightBatchTextureDrawing highlightDrawing;

		public TapDrawingTarget() : base()
		{
			redTexture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_tap_02.png");

			greenTexture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_extap_02.png");

			blueTexture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_hold_02.png");

			wallTexture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\walltap.png");

			tapExTexture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\tap_exEff.png");

			wallExTexture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\walltap_Eff.png");

			untagExTexture = ResourceUtils.OpenReadTextureFromResource(@"Modules\FumenVisualEditor\Views\OngekiObjects\mu3_nt_extap_01.png");

			void init(Texture texture)
			{
				normalList[texture] = new();
				selectTapList[texture] = new();
			}

			init(redTexture);
			init(greenTexture);
			init(blueTexture);
			init(wallTexture);
			init(untagExTexture);

			exList[tapExTexture] = new();
			exList[wallExTexture] = new();

			batchTextureDrawing = IoC.Get<IBatchTextureDrawing>();
			highlightDrawing = IoC.Get<IHighlightBatchTextureDrawing>();
		}

		public void Draw(IFumenEditorDrawingContext target, LaneType? laneType, OngekiMovableObjectBase tap, bool isCritical)
		{
			var texture = laneType switch
			{
				LaneType.Left => redTexture,
				LaneType.Center => greenTexture,
				LaneType.Right => blueTexture,
				LaneType.WallRight or LaneType.WallLeft => wallTexture,
				_ => untagExTexture
			};

			if (texture is null)
				return;

			var size = laneType switch
			{
				LaneType.WallRight => rightWallSize,
				LaneType.WallLeft => leftWallSize,
				_ => tapSize
			};

			var x = XGridCalculator.ConvertXGridToX(tap.XGrid, target.Editor);
			var y = target.ConvertToY(tap.TGrid);

			var pos = new Vector2((float)x, (float)y);
			normalList[texture].Add((size, pos, 0f));

			if (tap.IsSelected)
			{
				if (laneType == LaneType.WallLeft || laneType == LaneType.WallRight)
				{
					size = new(Math.Sign(size.X) * 42, 42);
				}
				else
				{
					size = tapSize * new Vector2(1.5f, 1.5f);
				}

				selectTapList[texture].Add((size, pos, 0f));
			}

			if (isCritical)
			{
				if (laneType == LaneType.WallLeft || laneType == LaneType.WallRight)
				{
					size = new(Math.Sign(size.X) * 39, 39);
					texture = wallExTexture;
				}
				else
				{
					size = new(68, 30);
					texture = tapExTexture;
				}

				exList[texture].Add((size, pos, 0f));
			}

			size.X = Math.Abs(size.X);
			target.RegisterSelectableObject(tap, pos, size);
		}

		private void ClearList()
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void clear(Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> map)
			{
				foreach (var list in map.Values)
					list.Clear();
			}

			clear(normalList);
			clear(exList);
			clear(selectTapList);
		}

		public void Dispose()
		{
			redTexture?.Dispose();
			greenTexture?.Dispose();
			blueTexture?.Dispose();
			wallTexture?.Dispose();
		}

		public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<Tap> objs)
		{
			foreach (var tap in objs)
				Draw(target, tap.ReferenceLaneStart?.LaneType, tap, tap.IsCritical);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void draw(Dictionary<Texture, List<(Vector2 size, Vector2 pos, float rotate)>> map)
			{
				foreach (var item in map)
					batchTextureDrawing.Draw(target, item.Key, item.Value);
			}

			foreach (var item in selectTapList)
				highlightDrawing.Draw(target, item.Key, item.Value);
			draw(exList);
			draw(normalList);

			ClearList();
		}
	}
}
