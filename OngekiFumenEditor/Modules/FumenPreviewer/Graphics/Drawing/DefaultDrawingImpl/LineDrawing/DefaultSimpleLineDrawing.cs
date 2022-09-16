using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
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

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.LineDrawing
{
    //[Export(typeof(ISimpleLineDrawing))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultSimpleLineDrawing : CommonDrawingBase, ISimpleLineDrawing, IDisposable
    {
        private readonly Shader shader;
        private readonly int vbo;
        private readonly int vao;

        public const int LINE_DRAW_MAX = 100;
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
                    GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * LINE_DRAW_MAX * 6),
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

        public void Draw(IFumenPreviewer target, IEnumerable<ILineDrawing.LineVertex> points, float lineWidth)
        {
            performenceMonitor.OnBeginDrawing(this);
            {
                GL.LineWidth(lineWidth);
                shader.Begin();
                {
                    shader.PassUniform("Model", GetOverrideModelMatrix());
                    shader.PassUniform("ViewProjection", target.ViewProjectionMatrix);
                    GL.BindVertexArray(vao);

                    var arrBuffer = ArrayPool<float>.Shared.Rent(LINE_DRAW_MAX * 6);
                    var arrBufferIdx = 0;

                    void Copy(ILineDrawing.LineVertex lp)
                    {
                        arrBuffer[(6 * arrBufferIdx) + 0] = lp.Point.X;
                        arrBuffer[(6 * arrBufferIdx) + 1] = lp.Point.Y;
                        arrBuffer[(6 * arrBufferIdx) + 2] = lp.Color.X;
                        arrBuffer[(6 * arrBufferIdx) + 3] = lp.Color.Y;
                        arrBuffer[(6 * arrBufferIdx) + 4] = lp.Color.Z;
                        arrBuffer[(6 * arrBufferIdx) + 5] = lp.Color.W;
                        arrBufferIdx++;
                    }

                    void FlushDraw()
                    {
                        //GL.InvalidateBufferData(vbo);
                        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, arrBufferIdx * sizeof(float) * 6, arrBuffer);
                        GL.DrawArrays(PrimitiveType.LineStrip, 0, arrBufferIdx);
                        performenceMonitor.CountDrawCall(this);

                        arrBufferIdx = 0;
                    }

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                    {
                        unsafe
                        {
                            var prevLinePoint = points.FirstOrDefault();

                            foreach (var item in points.SequenceWrap(LINE_DRAW_MAX - 1))
                            {
                                Copy(prevLinePoint);
                                foreach (var q in item)
                                {
                                    Copy(q);
                                    prevLinePoint = q;
                                }

                                FlushDraw();
                            }
                        }
                    }

                    ArrayPool<float>.Shared.Return(arrBuffer);
                    GL.BindVertexArray(0);
                }
                shader.End();
            }
            performenceMonitor.OnAfterDrawing(this);
        }

        public void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
        }
    }
}
