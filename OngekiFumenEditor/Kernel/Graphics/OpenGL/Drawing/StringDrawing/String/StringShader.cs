using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.StringDrawing.String
{
    public class StringShader : DefaultOpenGLShader
    {
        public StringShader(string vertexContent, string fragmentContent)
        {
            VertexProgram = vertexContent;
            FragmentProgram = fragmentContent;
        }
    }
}
