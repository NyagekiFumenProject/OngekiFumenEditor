using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.StringDrawing.String
{
    public class StringShader : Base.Shader
    {
        public StringShader(string vertexContent, string fragmentContent)
        {
            VertexProgram = vertexContent;
            FragmentProgram = fragmentContent;
        }
    }
}
