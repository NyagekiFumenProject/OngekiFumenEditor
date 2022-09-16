using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl
{
    public class CommonDrawingBase : IDrawing
    {
        private Stack<Matrix4> modelMatrices = new Stack<Matrix4>(new[] { Matrix4.Identity });

        public void PushOverrideModelMatrix(Matrix4 modelMatrix)
        {
            modelMatrices.Push(modelMatrix);
        }

        public bool PopOverrideModelMatrix(out Matrix4 modelMatrix)
        {
            modelMatrix = Matrix4.Identity;
            if (modelMatrices.Count <= 1)
                return false;

            modelMatrix = modelMatrices.Pop();
            return true;
        }

        public Matrix4 GetOverrideModelMatrix()
        {
            return modelMatrices.Peek();
        }
    }
}
