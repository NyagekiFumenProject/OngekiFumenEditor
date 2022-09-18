using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Windows.Documents;
using OngekiFumenEditor.Utils;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using ControlzEx.Standard;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String;
using static OngekiFumenEditor.Base.OngekiObjects.BulletPallete;
using System.Diagnostics;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.LineDrawing
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultSimpleLineDrawing : CommonDrawingBase, ISimpleLineDrawing, IDisposable
    {
        private readonly Shader shader;
        private readonly int vbo;
        private readonly int vao;

        public const int MAX_FILL_COUNT = 300000 / 6;

        private float[] postData = new float[MAX_FILL_COUNT * 6];
        private int postDataFillCount = 0;

        private IPerfomenceMonitor performenceMonitor;

        public DefaultSimpleLineDrawing()
        {
            performenceMonitor = IoC.Get<IPerfomenceMonitor>();
            shader = CommonLineShader.Shared;

            vbo = GL.GenBuffer();
            vao = GL.GenVertexArray();

            Init();
        }

        private void Init()
        {
            GL.BindVertexArray(vao);
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                {
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * MAX_FILL_COUNT * 6),
                        IntPtr.Zero, BufferUsageHint.DynamicCopy);

                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 6, 0);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(float) * 6, sizeof(float) * 2);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
            GL.Enable(EnableCap.LineSmooth);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
        }

        private IFumenPreviewer target = default;
        private float lineWidth = default;

        public void Begin(IFumenPreviewer target, float lineWidth)
        {
#if DEBUG
            Debug.Assert(target is null);
#endif
            performenceMonitor.OnBeginDrawing(this);
            this.target = target;
            this.lineWidth = lineWidth;
            GL.LineWidth(lineWidth);
            shader.Begin();
            shader.PassUniform("Model", GetOverrideModelMatrix());
            shader.PassUniform("ViewProjection", target.ViewProjectionMatrix);
            GL.BindVertexArray(vao);
        }

        void FlushDraw()
        {
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, postDataFillCount * sizeof(float) * 6, postData);
            GL.DrawArrays(PrimitiveType.LineStrip, 0, postDataFillCount);
            performenceMonitor.CountDrawCall(this);
            postDataFillCount = 0;
        }

        System.Numerics.Vector2 prevPoint = default;
        System.Numerics.Vector4 prevColor = default;

        public void PostPointInternal(System.Numerics.Vector2 Point, System.Numerics.Vector4 Color)
        {
            prevPoint = Point;
            prevColor = Color;

            postData[(6 * postDataFillCount) + 0] = Point.X;
            postData[(6 * postDataFillCount) + 1] = Point.Y;
            postData[(6 * postDataFillCount) + 2] = Color.X;
            postData[(6 * postDataFillCount) + 3] = Color.Y;
            postData[(6 * postDataFillCount) + 4] = Color.Z;
            postData[(6 * postDataFillCount) + 5] = Color.W;
            postDataFillCount++;
        }

        public void PostPoint(System.Numerics.Vector2 Point, System.Numerics.Vector4 Color)
        {
            if (postDataFillCount >= MAX_FILL_COUNT - 1)
            {
                FlushDraw();
                PostPointInternal(prevPoint, prevColor);
            }
            PostPointInternal(Point, Color);
        }

        public void End()
        {
            FlushDraw();
            GL.BindVertexArray(0);
            shader.End();
            target = default;
            performenceMonitor.OnAfterDrawing(this);
        }

        public void Draw(IFumenPreviewer target, IEnumerable<ILineDrawing.LineVertex> points, float lineWidth)
        {
            Begin(target, lineWidth);
            foreach (var point in points)
                PostPoint(point.Point, point.Color);
            End();
        }
    }
}
