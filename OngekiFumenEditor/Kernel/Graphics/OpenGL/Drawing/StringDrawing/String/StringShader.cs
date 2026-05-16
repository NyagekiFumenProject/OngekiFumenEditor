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
        private int textureSamplerLocation = int.MinValue;
        private int mvpLocation = int.MinValue;

        public StringShader(string vertexContent, string fragmentContent)
        {
            VertexProgram = vertexContent;
            FragmentProgram = fragmentContent;
        }

        public int TextureSamplerLocation => textureSamplerLocation == int.MinValue ? textureSamplerLocation = GetUniformLocation("TextureSampler") : textureSamplerLocation;

        public int MvpLocation => mvpLocation == int.MinValue ? mvpLocation = GetUniformLocation("MVP") : mvpLocation;
    }
}
