using Caliburn.Micro;
using FontStashSharp;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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

        private static readonly Dictionary<string, FSColor> colors = new()
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

        // Benchmark 显示原本每帧 GroupBy + 多层 ToListWithObjectPool 是分配大头,
        // 改为字典桶后时间 -22%~-54%、分配 -64%~-75% (见
        // docs/Performance_Issues.md #2 Benchmark 复核结论)。
        private static readonly IComparer<RegisterDrawingInfo> colorComparer =
            Comparer<RegisterDrawingInfo>.Create((a, b) =>
                colors[a.TimelineObject.IDShortName].PackedValue
                    .CompareTo(colors[b.TimelineObject.IDShortName].PackedValue));

        public override void DrawBatch(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, IEnumerable<OngekiTimelineObjectBase> objs)
        {
            using var buckets = ObjectPool.GetPooledDictionary<int, IPooledList<RegisterDrawingInfo>>();

            // 一次扫描进桶,替代 Select.ToList + GroupBy + 每组 ToList 的三层枚举与分配。
            foreach (var obj in objs)
            {
                var key = obj.TGrid.TotalGrid;
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = ObjectPool.GetPooledList<RegisterDrawingInfo>();
                    buckets[key] = bucket;
                }
                bucket.Add(new RegisterDrawingInfo(obj, target.ConvertToViewRelativeY_DefaultSoflanGroup(obj.TGrid)));
            }

            foreach (var kv in buckets)
            {
                var actualItems = kv.Value;
                try
                {
                    var tGrid = actualItems[0].TimelineObject.TGrid;
                    if (!target.CheckVisible(tGrid))
                    {
                        FilterToHeaderOnly(actualItems);
                        if (actualItems.Count == 0)
                            continue;
                    }

                    actualItems.Sort(colorComparer);

                    var y = (float)actualItems[0].Y;
                    using var lineVertices = ObjectPool.GetPooledList<LineVertex>();
                    var per = 1.0f * target.CurrentDrawingTargetContext.ViewRelativeRect.Width / actualItems.Count;
                    for (var i = 0; i < actualItems.Count; i++)
                    {
                        var c = colors[actualItems[i].TimelineObject.IDShortName];
                        var color = new Vector4(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
                        lineVertices.Add(new(new(per * i, y), color, VertexDash.Solider));
                        lineVertices.Add(new(new(per * (i + 1), y), color, VertexDash.Solider));
                    }
                    builder.DrawSimpleLines(lineVertices, 2);

                    DrawDescText(target, builder, y, actualItems);
                }
                finally
                {
                    actualItems.Dispose();
                }
            }
        }

        // CheckVisible 失败时,只保留 LaneBlockArea/Soflan 头尾指示器(原 RemoveAll 反向语义)。
        private static void FilterToHeaderOnly(IPooledList<RegisterDrawingInfo> items)
        {
            var writeIdx = 0;
            for (var readIdx = 0; readIdx < items.Count; readIdx++)
            {
                var keep = items[readIdx].TimelineObject is
                    LaneBlockArea or LaneBlockArea.LaneBlockAreaEndIndicator
                    or Soflan or Soflan.SoflanEndIndicator;
                if (!keep)
                    continue;
                if (writeIdx != readIdx)
                    items[writeIdx] = items[readIdx];
                writeIdx++;
            }
            while (items.Count > writeIdx)
                items.RemoveAt(items.Count - 1);
        }

        private void DrawDescText(IFumenEditorDrawingContext target, IDrawCommandListBuilder builder, float y, IPooledList<RegisterDrawingInfo> sortedItems)
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
            // sortedItems 已经按 color.PackedValue 排好序;不再 Select+OrderBy。
            for (var idx = 0; idx < sortedItems.Count; idx++)
            {
                var obj = sortedItems[idx].TimelineObject;
                var c = colors[obj.IDShortName];

                if (idx != 0)
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
            }
        }
    }
}
