using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors
{
	public class DrawTimeSignatureHelper
	{
		public struct CacheDrawTimeLineResult
		{
			public double Y { get; set; }
			public string Display { get; set; }
		}

		private List<CacheDrawTimeLineResult> drawLines = new();

		private IStringDrawing stringDrawing;
		private ILineDrawing lineDrawing;

		public DrawTimeSignatureHelper()
		{
			stringDrawing = IoC.Get<IStringDrawing>();
			lineDrawing = IoC.Get<ISimpleLineDrawing>();
		}

		public void DrawLines(IFumenEditorDrawingContext target)
		{
			drawLines.Clear();

			var fumen = target.Editor.Fumen;

			if (target.Editor.Setting.BeatSplit == 0)
				return;
			IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> timelines = Enumerable.Empty<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)>();
			if (target.Editor.IsDesignMode)
			{
				timelines = TGridCalculator.GetVisbleTimelines_DesignMode(
					fumen.Soflans,
					fumen.BpmList,
					fumen.MeterChanges,
					Math.Max(0, target.Rect.MinY),
					target.Rect.MaxY,
					target.Editor.Setting.JudgeLineOffsetY,
					target.Editor.Setting.BeatSplit,
					target.Editor.Setting.VerticalDisplayScale
				);
			}
			else
			{
				var currentY = TGridCalculator.ConvertAudioTimeToY_PreviewMode(target.CurrentPlayTime, target.Editor);
				timelines = TGridCalculator.GetVisbleTimelines_PreviewMode(
					fumen.Soflans,
					fumen.BpmList,
					fumen.MeterChanges,
					currentY,
					target.Rect.Height,
					target.Editor.Setting.JudgeLineOffsetY,
					target.Editor.Setting.BeatSplit,
					target.Editor.Setting.VerticalDisplayScale
				);
			}

			var transDisp = target.Rect.Width * 0.4f;
			var maxDispAlpha = 0.3f;
			var minDispAlpha = 0f;

			var isPreviewMode = target.Editor.IsPreviewMode;

			if (isPreviewMode)
				timelines = timelines.Where(x => x.beatIndex == 0);
			else
				minDispAlpha = maxDispAlpha;
			var eDisp = target.Rect.Width - transDisp;

			using var d = ObjectPool<List<LineVertex>>.GetWithUsingDisposable(out var list, out _);
			list.Clear();

			var displayAudioTime = target.Editor.Setting.DisplayTimeFormat == Models.EditorSetting.TimeFormat.AudioTime;


			foreach ((var t, var y, var beatIndex, _, _) in timelines)
			{
				var str = string.Empty;
				if (displayAudioTime)
				{
					var audioTime = TGridCalculator.ConvertTGridToAudioTime(t, target.Editor);
					str = $"{audioTime.Minutes,-2}:{audioTime.Seconds,-2}:{audioTime.Milliseconds,-3}";
				}
				else
					str = t.ToString();

				drawLines.Add(new()
				{
					Display = str,
					Y = y
				});

				var fy = (float)y;

				var maxAlpha = maxDispAlpha;
				var minAlpha = minDispAlpha;

				if (!isPreviewMode)
				{
					if(beatIndex == 0)
						maxAlpha = minAlpha = 1;
				}

				list.Add(new(new(0, fy), new(1, 1, 1, 0), VertexDash.Solider));
				list.Add(new(new(0, fy), new(1, 1, 1, maxAlpha), VertexDash.Solider));
				list.Add(new(new(transDisp, fy), new(1, 1, 1, minAlpha), VertexDash.Solider));
				list.Add(new(new(eDisp, fy), new(1, 1, 1, minAlpha), VertexDash.Solider));
				list.Add(new(new(target.ViewWidth, fy), new(1, 1, 1, maxAlpha), VertexDash.Solider));
				list.Add(new(new(target.ViewWidth, fy), new(1, 1, 1, 0), VertexDash.Solider));
			}

			lineDrawing.Draw(target, list, 1);
		}

		public void DrawTimeSigntureText(IFumenEditorDrawingContext target)
		{
			foreach (var pair in drawLines)
				stringDrawing.Draw(
					pair.Display,
					new(target.ViewWidth,
					(float)pair.Y + 10),
					Vector2.One,
					12,
					0,
					Vector4.One,
					new(1, 0.5f),
					IStringDrawing.StringStyle.Normal,
					target,
					default,
					out _
			);
		}
	}
}
