using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Shaders
{
    public class CommonSpriteShader : Shader
    {
        private static CommonSpriteShader _shared;

        public CommonSpriteShader()
        {
            VertexProgram = @"
                #version 330
out vec2 varying_texPos;

uniform mat4 Model;
uniform vec2 TextureSize;
uniform mat4 ViewProjection;

layout(location=0) in vec2 in_texPos;
layout(location=1) in vec2 in_pos;

void main(){
    vec2 v = in_pos;
	gl_Position = ViewProjection * Model * vec4(v.x,v.y,0.0,1.0);
	varying_texPos=in_texPos;
}
                ";
            FragmentProgram = @"
                #version 330
uniform sampler2D diffuse;
in vec2 varying_texPos;
out vec4 out_color;

void main(){
	out_color = texture(diffuse,varying_texPos);
}
                ";
        }

        public static CommonSpriteShader Shared => _shared ??= _createShared();

        private static CommonSpriteShader _createShared()
        {
            var shader = new CommonSpriteShader();
            shader.Compile();
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception($"Create shared CommonSpriteShader object failed ,OGL Error : {error}");
            return shader;
        }
    }
}
