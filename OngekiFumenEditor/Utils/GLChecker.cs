using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    internal class GLChecker
    {
        [Conditional("DEBUG")]
        public static void CheckError(string glApiName)
        {
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception($"Call API {glApiName} failed ,OGL Error : {error}");
        }
    }
}
