using OngekiFumenEditor.Base.OngekiObjects.BulletPalleteEnums;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;
using OpenTK.Mathematics;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics
{
    public class CommonDrawingBase : IDrawing
    {
        private Stack<Matrix4> modelMatrices = new Stack<Matrix4>([Matrix4.Identity]);
        private Stack<Matrix4> viewMatrices = new Stack<Matrix4>();
        private Stack<Matrix4> projectionMatrices = new Stack<Matrix4>();

        #region Model Matrix

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushOverrideModelMatrix(Matrix4 modelMatrix)
        {
            modelMatrices.Push(modelMatrix);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PopOverrideModelMatrix(out Matrix4 modelMatrix)
        {
            modelMatrix = Matrix4.Identity;
            if (modelMatrices.Count == 0)
                return false;

            modelMatrix = modelMatrices.Pop();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4 GetOverrideModelMatrix()
        {
            return modelMatrices.Peek();
        }

        #endregion

        #region View Matrix

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushOverrideViewMatrix(Matrix4 viewMatrix)
        {
            viewMatrices.Push(viewMatrix);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PopOverrideViewMatrix(out Matrix4 viewMatrix)
        {
            viewMatrix = Matrix4.Identity;
            if (viewMatrices.Count == 0)
                return false;

            viewMatrix = viewMatrices.Pop();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4 GetOverrideViewMatrixOrDefault(DrawingTargetContext ctx)
        {
            return viewMatrices.Count == 0 ? ctx.ViewMatrix : viewMatrices.Peek();
        }

        #endregion

        #region Projection Matrix

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PushOverrideProjectionMatrix(Matrix4 projectionMatrix)
        {
            projectionMatrices.Push(projectionMatrix);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PopOverrideProjectionMatrix(out Matrix4 projectionMatrix)
        {
            projectionMatrix = Matrix4.Identity;
            if (projectionMatrices.Count <= 1)
                return false;

            projectionMatrix = projectionMatrices.Pop();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4 GetOverrideProjectionMatrixOrDefault(DrawingTargetContext ctx)
        {
            return projectionMatrices.Count == 0 ? ctx.ProjectionMatrix : projectionMatrices.Peek();
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4 GetOverrideViewProjectMatrixOrDefault(DrawingTargetContext ctx)
        {
            var v = GetOverrideViewMatrixOrDefault(ctx);
            var p = GetOverrideProjectionMatrixOrDefault(ctx);
            var vp = v * p;
            return vp;
        }
    }
}
