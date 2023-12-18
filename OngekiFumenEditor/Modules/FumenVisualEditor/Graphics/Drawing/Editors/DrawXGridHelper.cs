using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
	public class DrawXGridHelper
	{
		public struct CacheDrawXLineResult
		{
			public float X { get; set; }
			public float XGridTotalUnit { get; set; }
			public string XGridTotalUnitDisplay { get; set; }

			public override string ToString() => $"X:{X:F3} XGridTotalUnit:{XGridTotalUnit:F3} Display:{XGridTotalUnitDisplay}";
		}

		private IStringDrawing stringDrawing;
		private ILineDrawing lineDrawing;

		public DrawXGridHelper()
		{
			stringDrawing = IoC.Get<IStringDrawing>();
			lineDrawing = IoC.Get<ISimpleLineDrawing>();
		}

		public void DrawLines(IFumenEditorDrawingContext target, IEnumerable<CacheDrawXLineResult> drawLines)
		{
			if (target.Editor.IsPreviewMode)
				return;

			using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);
			list.Clear();

			foreach (var result in drawLines)
			{
				var a = Math.Abs(result.XGridTotalUnit) switch
				{
					0 => 0.4f,
					24 => 0.6f,
					_ => 0.25f
				};

				list.Add(new(new(result.X, target.Rect.Height), new(1, 1, 1, 0), VertexDash.Solider));
				list.Add(new(new(result.X, 0), new(1, 1, 1, a), VertexDash.Solider));
				list.Add(new(new(result.X, 0 + target.Rect.Height), new(1, 1, 1, a), VertexDash.Solider));
				list.Add(new(new(result.X, 0 + target.Rect.Height), new(1, 1, 1, 0), VertexDash.Solider));
			}

			lineDrawing.PushOverrideViewProjectMatrix(OpenTK.Mathematics.Matrix4.CreateTranslation(-target.ViewWidth / 2, -target.ViewHeight / 2, 0) * target.ProjectionMatrix);
			lineDrawing.Draw(target, list, 1);
			lineDrawing.PopOverrideViewProjectMatrix(out _);
		}

		public void DrawXGridText(IFumenEditorDrawingContext target, IEnumerable<CacheDrawXLineResult> drawLines)
		{
			if (target.Editor.IsPreviewMode)
				return;

			foreach (var pair in drawLines)
				stringDrawing.Draw(
					pair.XGridTotalUnitDisplay,
					new(pair.X,
					target.Rect.MaxY),
					Vector2.One,
					12,
					0,
					Vector4.One,
					new(0, 0f),
					IStringDrawing.StringStyle.Normal,
					target,
					default,
					out _
			);
		}
	}
}
