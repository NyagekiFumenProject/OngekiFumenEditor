using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.TextureDrawing
{
	internal class BatchShader : DefaultOpenGLShader
	{
		private int viewProjectionLocation = int.MinValue;
		private int diffuseLocation = int.MinValue;

		public BatchShader()
		{
			VertexProgram = @"
                #version 330
                out vec4 varying_color;
                out vec2 varying_texPos;
                uniform mat4 ViewProjection;
                layout(location=0) in vec2 in_texPos;
                layout(location=1) in vec2 in_pos;
                layout(location=2) in vec2 in_size;
                layout(location=3) in vec2 in_position;
                layout(location=4) in float in_rotation;
                layout(location=5) in vec4 in_color;
                void main(){
                    float s = sin(in_rotation);
                    float c = cos(in_rotation);
                    vec2 scaledPos = in_pos * in_size;
                    vec2 rotatedPos = vec2(
                        scaledPos.x * c - scaledPos.y * s,
                        scaledPos.x * s + scaledPos.y * c
                    );
	                gl_Position=ViewProjection * vec4(rotatedPos + in_position,0.0,1.0);
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

		public int ViewProjectionLocation => viewProjectionLocation == int.MinValue ? viewProjectionLocation = GetUniformLocation("ViewProjection") : viewProjectionLocation;

		public int DiffuseLocation => diffuseLocation == int.MinValue ? diffuseLocation = GetUniformLocation("diffuse") : diffuseLocation;
	}
}
