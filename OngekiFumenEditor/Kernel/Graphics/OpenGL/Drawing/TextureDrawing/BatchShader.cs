using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.TextureDrawing
{
    internal class BatchShader : DefaultOpenGLShader
	{
		public BatchShader()
		{
			VertexProgram = @"
                #version 330
                out vec4 varying_color;
                out vec2 varying_texPos;
                uniform mat4 ViewProjection;
                layout(location=0) in vec2 in_texPos;
                layout(location=1) in vec2 in_pos;
                layout(location=2) in mat4 in_model;
                layout(location=6) in vec4 in_color;
                void main(){
	                gl_Position=ViewProjection * in_model * vec4(in_pos,0.0,1.0);
	                varying_texPos=in_texPos;
                    varying_color = in_color;
                }   
                ";
			FragmentProgram = @"
                #version 330
                uniform sampler2D diffuse;
                in vec2 varying_texPos;
                in vec4 varying_color;
                out vec4 out_color;
                void main(){
	                vec4 texColor=texture(diffuse,varying_texPos);
	                out_color=texColor * varying_color;
                }
                ";
		}
	}
}
