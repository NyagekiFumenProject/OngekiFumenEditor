
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.BeamDrawing
{
    internal class BeamLazerShader : DefaultOpenGLShader
	{
		private int modelLocation = int.MinValue;
		private int viewProjectionLocation = int.MinValue;
		private int textureScaleYLocation = int.MinValue;
		private int colorLocation = int.MinValue;
		private int progressLocation = int.MinValue;
		private int diffuseLocation = int.MinValue;

		public BeamLazerShader()
		{
			VertexProgram = @"
                #version 330
out vec2 varying_texPos;

uniform mat4 Model;
uniform mat4 ViewProjection;
uniform float progress;

layout(location=0) in vec2 in_texPos;
layout(location=1) in vec2 in_pos;

void main(){
    float w =  smoothstep(-1, 0, progress) * (1 - smoothstep(1, 2, progress));
	gl_Position = ViewProjection * Model * vec4(in_pos /* * vec2(w,1.0f)*/,0.0,1.0);
	varying_texPos=in_texPos;
}
                ";
			FragmentProgram = @"
                #version 330
uniform sampler2D diffuse;
uniform vec4 color;
uniform float textureScaleY;
uniform float progress;
in vec2 varying_texPos;
out vec4 out_color;

void main(){
    vec2 tp = vec2(varying_texPos.x,varying_texPos.y * textureScaleY);
    float a =  smoothstep(-1, 0, progress) * (1 - smoothstep(1, 2, progress));
	out_color = vec4(texture(diffuse,tp).rgb,a) * color;
}
                ";
		}

		public int ModelLocation => modelLocation == int.MinValue ? modelLocation = GetUniformLocation("Model") : modelLocation;

		public int ViewProjectionLocation => viewProjectionLocation == int.MinValue ? viewProjectionLocation = GetUniformLocation("ViewProjection") : viewProjectionLocation;

		public int TextureScaleYLocation => textureScaleYLocation == int.MinValue ? textureScaleYLocation = GetUniformLocation("textureScaleY") : textureScaleYLocation;

		public int ColorLocation => colorLocation == int.MinValue ? colorLocation = GetUniformLocation("color") : colorLocation;

		public int ProgressLocation => progressLocation == int.MinValue ? progressLocation = GetUniformLocation("progress") : progressLocation;

		public int DiffuseLocation => diffuseLocation == int.MinValue ? diffuseLocation = GetUniformLocation("diffuse") : diffuseLocation;
	}
}
