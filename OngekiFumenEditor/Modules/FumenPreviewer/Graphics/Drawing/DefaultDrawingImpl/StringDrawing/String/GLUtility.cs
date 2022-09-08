using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String
{
    internal static class GLUtility
    {
        public static void CheckError()
        {
#if DEBUG
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception("GL.GetError() returned " + error.ToString());
#endif
        }
    }
}
