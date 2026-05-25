using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Utils;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    [Export(typeof(IFumenEditorDrawingTarget))]
    public sealed class CommonHorizonalDrawingTarget : CommonBatchDrawTargetBase<OngekiTimelineObjectBase>
    {
        public record RegisterDrawingInfo(OngekiTimelineObjectBase TimelineObject, double Y);

        public override int DefaultRenderOrder => 1500;
        public override DrawingVisible DefaultVisible => DrawingVisible.Design; //only design

        public override void Initialize(IRenderManagerImpl impl)
        {
        }

        public override IEnumerable<string> DrawTargetID { get; } =
        [
            "MET","BPM","EST","CLK","LBK","[LBK_End]","[CMT]"
        ];

        private Dictionary<string, FSColor> colors = new()
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

        public override void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<OngekiTimelineObjectBase> objs)
        {
            using var objects = objs.Select(x => new RegisterDrawingInfo(x, target.ConvertToY_DefaultSoflanGroup(x.TGrid))).ToListWithObjectPool();

            foreach (var g in objects.GroupBy(x => x.TimelineObject.TGrid.TotalGrid))
            {
                var tGrid = g.FirstOrDefault().TimelineObject.TGrid;
                using var actualItems = g.ToListWithObjectPool();
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
                using var regColors = actualItems.Select(x => colors[x.TimelineObject.IDShortName]).OrderBy(x => x.PackedValue).ToListWithObjectPool();
                var per = 1.0f * target.CurrentDrawingTargetContext.Rect.Width / regColors.Count;
                using var lineVertices = ObjectPool.GetPooledList<LineVertex>();
                for (int i = 0; i < regColors.Count; i++)
                {
                    var c = regColors[i];
                    var color = new Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
                    lineVertices.Add(new(new(per * i, y), color, VertexDash.Solider));
                    lineVertices.Add(new(new(per * (i + 1), y), color, VertexDash.Solider));
                }
                builder.DrawSimpleLines(lineVertices, 2);

                //draw range line if need
                foreach (var obj in actualItems)
                {
                    switch (obj.TimelineObject)
                    {
                        default:
                            break;
                    }
                }

                DrawDescText(target, builder, y, actualItems);
            }
        }

        private void DrawDescText(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, float y, IEnumerable<RegisterDrawingInfo> group)
        {
            string formatObj(OngekiObjectBase s) => s switch
            {
                BPMChange o => $"BPM:{(int)o.BPM}",
                Comment o => $"Comment:{o.Content}",
                MeterChange o => $"MET:{o.BunShi}/{o.Bunbo}",
                InterpolatableSoflan o => $"[{o.SoflanGroup}]I-SPD:({o.Easing}){o.Speed:F2}x",
                Soflan o => $"[{o.SoflanGroup}]D-SPD:{o.Speed:F2}x",
                KeyframeSoflan o => $"[{o.SoflanGroup}]K-SPD:{o.Speed:F2}x",
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
                    var slash = "/";
                    var slashSize = builder.MeasureString(slash, Vector2.One, 16, FontStyle.Normal, default);
                    builder.DrawString(
                        slash,
                        new Vector2(x, y + 12),
                        Vector2.One, 16, 0,
                        Vector4.One,
                        new(0, 0.5f),
                        FontStyle.Normal,
                        default);

                    x += slashSize.X;
                }

                var text = " " + formatObj(obj) + " ";
                var fontColor = new Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
                var size = builder.MeasureString(text, Vector2.One, 16, FontStyle.Normal, default);
                builder.DrawString(
                    text,
                    new Vector2(x, y + 12),
                    Vector2.One, 16, 0,
                    fontColor,
                    new(0, 0.5f),
                    FontStyle.Normal,
                    default);
                var borderPos = new Vector2(x + size.X / 2, y + size.Y / 2 + 1);

                target.RegisterSelectableObject(obj, borderPos, size);
                if (obj.IsSelected)
                {
                    var bx = borderPos.X;
                    var by = borderPos.Y;
                    var hw = size.X / 2;
                    var hh = size.Y / 2;
                    var color = new Vector4(1, 1, 0, 1);

                    builder.DrawSimpleLines(new[]
                    {
                        new LineVertex(new(bx - hw, by + hh), color, VertexDash.Solider),
                        new LineVertex(new(bx + hw, by + hh), color, VertexDash.Solider),
                        new LineVertex(new(bx + hw, by - hh), color, VertexDash.Solider),
                        new LineVertex(new(bx - hw, by - hh), color, VertexDash.Solider),
                        new LineVertex(new(bx - hw, by + hh), color, VertexDash.Solider),
                    }, 1);
                }
                x += size.X;
                i++;
            }
        }
    }
}
