using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Kernel.Graphics.Text;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing
{
    /// <summary>仅为当前 ISA 物件批次混入组色，不改变透明度、几何或命中范围。</summary>
    public sealed class SoflanGroupTintBuilder : IDrawCommandListBuilder
    {
        private IDrawCommandListBuilder inner;
        private Vector4 tint;
        private readonly List<ILineDrawing.LineVertex> lines = new();
        private readonly List<TextureInstance> textures = new();
        private readonly List<CircleInstance> circles = new();
        private readonly List<PolygonVertex> polygons = new();

        public void Reset(IDrawCommandListBuilder builder, Vector4 color)
        {
            ArgumentNullException.ThrowIfNull(builder);
            inner = builder is SoflanGroupTintBuilder existing ? existing.inner : builder;
            tint = color;
        }

        // 内层立即复制到命令池，同一绘制线程可复用暂存集合。
        private List<ILineDrawing.LineVertex> TintLines(IEnumerable<ILineDrawing.LineVertex> source)
        {
            lines.Clear();
            foreach (var x in source)
                lines.Add(new(x.Point, TintColor(x.Color), x.Dash));
            return lines;
        }

        private List<TextureInstance> TintTextures(IEnumerable<TextureInstance> source)
        {
            textures.Clear();
            foreach (var x in source)
                textures.Add(new(x.Size, x.Position, x.Rotation, TintColor(x.Color)));
            return textures;
        }

        private List<CircleInstance> TintCircles(IEnumerable<CircleInstance> source)
        {
            circles.Clear();
            foreach (var x in source)
                circles.Add(new(x.Point, TintColor(x.Color), x.IsSolid, x.Radius, x.HollowLineWidth));
            return circles;
        }

        private List<PolygonVertex> TintPolygons(IEnumerable<PolygonVertex> source)
        {
            polygons.Clear();
            foreach (var x in source)
                polygons.Add(new(x.Point, TintColor(x.Color)));
            return polygons;
        }

        public SoflanGroupTintBuilder(IDrawCommandListBuilder inner, Vector4 tint)
        {
            ArgumentNullException.ThrowIfNull(inner);
            // 嵌套绘制重新选择组色时只保留一层滤镜，避免重复混色。
            this.inner = inner is SoflanGroupTintBuilder existing ? existing.inner : inner;
            this.tint = tint;
        }

        public Vector4 TintColor(Vector4 color)
        {
            var result = Vector4.Lerp(color, tint, 0.15f);
            result.W = color.W;
            return result;
        }

        public void SetCleanColor(Vector4? color) => inner.SetCleanColor(color);
        public void SetViewport(float viewWidth, float viewHeight, float renderScaleX = 1, float renderScaleY = 1) => inner.SetViewport(viewWidth, viewHeight, renderScaleX, renderScaleY);
        public void SetCurrentModelMatrix(Matrix4x4 matrix) => inner.SetCurrentModelMatrix(matrix);
        public void SetCurrentViewMatrix(Matrix4x4 matrix) => inner.SetCurrentViewMatrix(matrix);
        public void SetCurrentProjectionMatrix(Matrix4x4 matrix) => inner.SetCurrentProjectionMatrix(matrix);
        public void SetCurrentRect(VisibleRect rect) => inner.SetCurrentRect(rect);
        public void PushModelMatrix(Matrix4x4 matrix) => inner.PushModelMatrix(matrix);
        public void PushViewMatrix(Matrix4x4 matrix) => inner.PushViewMatrix(matrix);
        public void PushProjectionMatrix(Matrix4x4 matrix) => inner.PushProjectionMatrix(matrix);
        public void PopModelMatrix() => inner.PopModelMatrix();
        public void PopViewMatrix() => inner.PopViewMatrix();
        public void PopProjectionMatrix() => inner.PopProjectionMatrix();
        public void DrawLines(IEnumerable<ILineDrawing.LineVertex> points, float lineWidth) => inner.DrawLines(TintLines(points), lineWidth);
        public void DrawSimpleLines(IEnumerable<ILineDrawing.LineVertex> points, float lineWidth) => inner.DrawSimpleLines(TintLines(points), lineWidth);
        public void DrawTexture(IImage texture, IEnumerable<TextureInstance> instances) => inner.DrawTexture(texture, TintTextures(instances));
        public void DrawBatchTexture(IImage texture, IEnumerable<TextureInstance> instances) => inner.DrawBatchTexture(texture, TintTextures(instances));
        // 选中高亮和文字标签保留原色，保证辨识度。
        public void DrawHighlightBatchTexture(IImage texture, IEnumerable<TextureInstance> instances) => inner.DrawHighlightBatchTexture(texture, instances);
        public void DrawCircle(Vector2 point, Vector4 color, bool isSolid, float radius, float hollowLineWidth = 0) => inner.DrawCircle(point, TintColor(color), isSolid, radius, hollowLineWidth);
        public void DrawCircles(IEnumerable<CircleInstance> instances) => inner.DrawCircles(TintCircles(instances));
        public void DrawPolygon(Primitive primitive, IEnumerable<PolygonVertex> vertices) => inner.DrawPolygon(primitive, TintPolygons(vertices));
        public void DrawString(string text, Vector2 pos, Vector2 scale, int fontSize, float rotate, Vector4 color, Vector2 origin, FontStyle style, IFontHandle handle) => inner.DrawString(text, pos, scale, fontSize, rotate, color, origin, style, handle);
        public Vector2 MeasureString(string text, Vector2 scale, int fontSize, FontStyle style, IFontHandle handle) => inner.MeasureString(text, scale, fontSize, style, handle);
        public void DrawBeam(IImage texture, int width, float x, float progress, Vector4 color, float rotate, float judgeOffset) => inner.DrawBeam(texture, width, x, progress, TintColor(color), rotate, judgeOffset);
        // 已生成的命令可能已带组色；原样转发，避免重放时重复加滤镜。
        // 当前物件绘制器通过上面的即时绘图入口生成命令，不使用此缓存重放入口。
        public void DrawDrawCommandList(IEnumerable<DrawCommand> drawCommands) => inner.DrawDrawCommandList(drawCommands);
        public DrawCommandList GetDrawCommandList() => inner.GetDrawCommandList();
        public void Clear() => inner.Clear();
        // 装饰器不拥有帧级 builder，其生命周期仍由调用者管理。
        public void Dispose()
        {
        }
    }
}
