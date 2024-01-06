using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
	[Export(typeof(IFumenEditorDrawingTarget))]
	public class CommonHorizonalDrawingTarget : CommonBatchDrawTargetBase<OngekiTimelineObjectBase>
	{
		public record RegisterDrawingInfo(OngekiTimelineObjectBase TimelineObject, double Y);

		public override int DefaultRenderOrder => 1500;
		public override DrawingVisible DefaultVisible => DrawingVisible.Design; //only design

		private IStringDrawing stringDrawing;
		private ISimpleLineDrawing lineDrawing;

		public CommonHorizonalDrawingTarget()
		{
			lineDrawing = IoC.Get<ISimpleLineDrawing>();
			stringDrawing = IoC.Get<IStringDrawing>();
		}

		public override IEnumerable<string> DrawTargetID { get; } = new[]
		{
			"MET","SFL","BPM","EST","CLK","LBK","[LBK_End]","[SFL_End]","[CMT]","[INTP_SFL]","[INTP_SFL_End]","[KEY_SFL]"
		};

		private static Dictionary<string, FSColor> colors = new()
		{
			{"MET", FSColor.LightGreen },
			{"SFL", FSColor.LightCyan },
			{"[CMT]", FSColor.Crimson },
			{"[INTP_SFL]", FSColor.LightSeaGreen },
			{"[KEY_SFL]", FSColor.Cornsilk },
			{"[INTP_SFL_End]", FSColor.LightSeaGreen },
			{"BPM", FSColor.Pink },
			{"EST", FSColor.Yellow },
			{"CLK", FSColor.CadetBlue },
			{"LBK", FSColor.HotPink },
			{"[LBK_End]", FSColor.HotPink },
			{"[SFL_End]", FSColor.LightCyan },
		};

		public override void DrawBatch(IFumenEditorDrawingContext target, IEnumerable<OngekiTimelineObjectBase> objs)
		{
			using var d4 = objs.Select(x => new RegisterDrawingInfo(x, target.ConvertToY(x.TGrid))).ToListWithObjectPool(out var objects);

			foreach (var g in objects.GroupBy(x => x.TimelineObject.TGrid.TotalGrid))
			{
				var tGrid = g.FirstOrDefault().TimelineObject.TGrid;
				using var d3 = g.ToListWithObjectPool(out var actualItems);
				if (!target.CheckVisible(tGrid))
				{
					actualItems.RemoveAll(x => x.TimelineObject switch
					{
						LaneBlockArea or LaneBlockArea.LaneBlockAreaEndIndicator or Soflan or Soflan.SoflanEndIndicator => false,
						_ => true
					});
					if (actualItems.Count == 0)
						continue;
				}

				var y = (float)g.FirstOrDefault().Y;
				using var d = actualItems.Select(x => colors[x.TimelineObject.IDShortName]).OrderBy(x => x.PackedValue).ToListWithObjectPool(out var regColors);
				var per = 1.0f * target.ViewWidth / regColors.Count;
				lineDrawing.Begin(target, 2);
				for (int i = 0; i < regColors.Count; i++)
				{
					var c = regColors[i];
					lineDrawing.PostPoint(new(per * i, y), new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f), VertexDash.Solider);
					lineDrawing.PostPoint(new(per * (i + 1), y), new(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f), VertexDash.Solider);
				}
				lineDrawing.End();

				//draw range line if need
				foreach (var obj in actualItems)
				{
					switch (obj.TimelineObject)
					{
						default:
							break;
					}
				}

				DrawDescText(target, y, actualItems);
			}
		}

		private void DrawDescText(IFumenEditorDrawingContext target, float y, IEnumerable<RegisterDrawingInfo> group)
		{
			string formatObj(OngekiObjectBase s) => s switch
			{
				BPMChange o => $"BPM:{(int)o.BPM}",
				Comment o => $"Comment:{o.Content}",
				MeterChange o => $"MET:{o.BunShi}/{o.Bunbo}",
				InterpolatableSoflan o => $"I-SPD:({o.Easing}){o.Speed:F2}x",
				Soflan o => $"D-SPD:{o.Speed:F2}x",
				KeyframeSoflan o => $"K-SPD:{o.Speed:F2}x",
				InterpolatableSoflan.InterpolatableSoflanIndicator o => $"{formatObj(o.RefSoflan)}_End -> {o.Speed:F2}x",
				Soflan.SoflanEndIndicator o => $"{formatObj(o.RefSoflan)}_End",
				LaneBlockArea o => $"LBK:{o.Direction}",
				LaneBlockArea.LaneBlockAreaEndIndicator o => $"{formatObj(o.RefLaneBlockArea)}_End",
				EnemySet o => $"EST:{o.TagTblValue}",
				ClickSE o => $"CLK",
				_ => string.Empty
			};

			var x = 0f;
			var i = 0;
			foreach ((var obj, var c) in group.Select(x => (x.TimelineObject, colors[x.TimelineObject.IDShortName])).OrderBy(x => x.Item2.PackedValue))
			{
				if (i != 0)
				{
					stringDrawing.Draw(
					"/",
					new Vector2(x, y + 12),
					Vector2.One, 16, 0,
					Vector4.One,
					new(0, 0.5f),
					IStringDrawing.StringStyle.Normal,
					target,
					default,
					out var s);

					x += s.Value.X;
				}

				var text = " " + formatObj(obj) + " ";
				var fontColor = new Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
				stringDrawing.Draw(
					text,
					new Vector2(x, y + 12),
					Vector2.One, 16, 0,
					fontColor,
					new(0, 0.5f),
					IStringDrawing.StringStyle.Normal,
					target,
					default,
					out var size);
				var borderPos = new Vector2(x + size.Value.X / 2, y + size.Value.Y / 2 + 1);

				target.RegisterSelectableObject(obj, borderPos, size ?? default);
				if (obj.IsSelected)
				{
					var bx = borderPos.X;
					var by = borderPos.Y;
					var hw = size.Value.X / 2;
					var hh = size.Value.Y / 2;

					lineDrawing.Begin(target, 1);
					lineDrawing.PostPoint(new(bx - hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider);
					lineDrawing.PostPoint(new(bx + hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider);
					lineDrawing.PostPoint(new(bx + hw, by - hh), new(1, 1, 0, 1), VertexDash.Solider);
					lineDrawing.PostPoint(new(bx - hw, by - hh), new(1, 1, 0, 1), VertexDash.Solider);
					lineDrawing.PostPoint(new(bx - hw, by + hh), new(1, 1, 0, 1), VertexDash.Solider);
					lineDrawing.End();
				}
				x += size.Value.X;
				i++;
			}
		}
	}
}
