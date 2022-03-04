namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing
{
    internal class BatchShader : Shader
    {
        public BatchShader()
        {
            this.VertexProgram = @"
                #version 330
                out vec4 varying_color;
                out vec2 varying_texPos;
                uniform mat4 ViewProjection;
                layout(location=0) in vec2 in_texPos;
                layout(location=1) in vec2 in_pos;
                layout(location=2) in vec2 in_anchor;
                layout(location=3) in vec4 in_color;
                layout(location=4) in vec2 in_flip;
                layout(location=5) in mat3x2 in_model;
                void main(){
                    vec2 v = in_model*vec3(in_pos*in_flip-in_anchor,1.0);
	                gl_Position=ViewProjection*vec4(v.x,v.y,0.0,1.0);
	                varying_color=in_color;
	                varying_texPos=in_texPos;
                }
                ";
            this.FragmentProgram = @"
                #version 330
                uniform sampler2D diffuse;
                in vec4 varying_color;
                in vec2 varying_texPos;
                out vec4 out_color;
                void main(){
	                vec4 texColor=texture(diffuse,varying_texPos);
	                out_color=(varying_color*texColor);
                }
                ";
        }
    }

    internal class DebugBatchShader : Shader
    {
        public DebugBatchShader()
        {
            this.VertexProgram = @"
#version 330
out vec4 varying_color;
out vec2 varying_texPos;
uniform mat4 ViewProjection;
layout(location=0) in vec2 in_texPos;
layout(location=1) in vec2 in_pos;
layout(location=2) in float in_Z;
layout(location=3) in vec2 in_anchor;
layout(location=4) in vec4 in_color;
layout(location=5) in vec2 in_bound;
layout(location=6) in mat4 in_model;
void main(){
	gl_Position=ViewProjection*in_model*vec4((in_pos-in_anchor)*in_bound,in_Z,1.0);
	varying_color=in_color;
	varying_texPos=in_texPos;
}
";
            this.FragmentProgram = @"
#version 330
uniform sampler2D diffuse;
in vec4 varying_color;
in vec2 varying_texPos;
out vec4 out_color;
void main(){
	vec4 texColor=texture(diffuse,vec2(varying_texPos.x,-varying_texPos.y));
	out_color=(varying_color*texColor);
	if(varying_texPos.x<0.05||varying_texPos.y<0.05||varying_texPos.x>0.95||varying_texPos.y>0.95)
		out_color=vec4(vec3(1,1,1)-out_color.rgb,out_color.a+0.2);
}
";
        }
    }
}