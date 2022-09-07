﻿using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Polyline2DCSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    public abstract class CommonCapLinesDrawTargetBase<T> : CommonDrawTargetBase<T>, IDisposable where T : OngekiObjectBase
    {
        public record LinePoint(Vector2 Point, Vector4 Color);

        private readonly Shader shader;
        private readonly int vbo;
        private readonly int vao;
        private bool backup_ps;
        private int backup_ps_hint;

        public int LineWidth { get; set; } = 2;

        public CommonCapLinesDrawTargetBase()
        {
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
                    GL.EnableVertexAttribArray(0);
                    GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * 6, 0);

                    GL.EnableVertexAttribArray(1);
                    GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(float) * 6, sizeof(float) * 2);
                }
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }
            GL.BindVertexArray(0);
        }

        public override void BeginDraw()
        {
            base.BeginDraw();

            backup_ps = GL.IsEnabled(EnableCap.PolygonSmooth);
            backup_ps_hint = GL.GetInteger(GetPName.PolygonSmoothHint);

            GL.Enable(EnableCap.PolygonSmooth);
            GL.Hint(HintTarget.PolygonSmoothHint, HintMode.Nicest);
        }

        public override void EndDraw()
        {
            base.EndDraw();
            if (backup_ps)
                GL.Enable(EnableCap.PolygonSmooth);
            else
                GL.Disable(EnableCap.PolygonSmooth);
            GL.Hint(HintTarget.PolygonSmoothHint, (HintMode)backup_ps_hint);
        }

        public abstract void FillLine(List<LinePoint> list, T obj, OngekiFumen fumen);

        public override void Draw(T obj, OngekiFumen fumen)
        {
            using var d = ObjectPool<List<LinePoint>>.GetWithUsingDisposable(out var list, out _);
            list.Clear();
            FillLine(list, obj, fumen);

            Draw(list, LineWidth);
        }

        public void Draw(List<LinePoint> list, int lineWidth)
        {
            if (list.Count == 0)
                return;

            shader.Begin();
            shader.PassUniform("Model", Matrix4.CreateTranslation(-Previewer.ViewWidth / 2, -Previewer.ViewHeight / 2, 0));
            shader.PassUniform("ViewProjection", Previewer.ViewProjectionMatrix);
            GL.BindVertexArray(vao);

            using var d = ObjectPool<List<Vec2>>.GetWithUsingDisposable(out var vecList, out _);
            vecList.Clear();

            var color = list.FirstOrDefault().Color;

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            {
                using var d2 = list.Select(x => new Vec2() { x = x.Point.X, y = x.Point.Y }).ToListWithObjectPool(out var inputVecList);

                var points = Polyline2D.Create(vecList, inputVecList, lineWidth,
                    Polyline2D.JointStyle.ROUND,
                    Polyline2D.EndCapStyle.ROUND
                    );


                var arrBuffer2 = ArrayPool<float>.Shared.Rent(points.Count * 6);
                var arrBufferIdx2 = 0;

                foreach (var p in points)
                {
                    arrBuffer2[(6 * arrBufferIdx2) + 0] = p.x;
                    arrBuffer2[(6 * arrBufferIdx2) + 1] = p.y;
                    arrBuffer2[(6 * arrBufferIdx2) + 2] = color.X;
                    arrBuffer2[(6 * arrBufferIdx2) + 3] = color.Y;
                    arrBuffer2[(6 * arrBufferIdx2) + 4] = color.Z;
                    arrBuffer2[(6 * arrBufferIdx2) + 5] = color.W;

                    arrBufferIdx2++;
                }

                GL.InvalidateBufferData(vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(sizeof(float) * arrBufferIdx2 * 6), arrBuffer2, BufferUsageHint.DynamicDraw);
                GL.DrawArrays(PrimitiveType.Triangles, 0, arrBufferIdx2);

                ArrayPool<float>.Shared.Return(arrBuffer2);
            }

            GL.BindVertexArray(0);
            shader.End();
        }

        public virtual void Dispose()
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
        }
    }
}