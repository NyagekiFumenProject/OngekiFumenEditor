namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.TextureDrawing
{
	internal class HighlightBatchShader : BatchShader
	{
		public HighlightBatchShader() : base()
		{
			FragmentProgram = @"
#version 330
                uniform sampler2D diffuse;
                uniform vec2 iResolution;
                in vec2 varying_texPos;
                out vec4 out_color;
void main(){
    float Pi = 6.28318530718; // Pi*2
    
    // GAUSSIAN BLUR SETTINGS {{{
    float Directions = 16.0; // BLUR DIRECTIONS (Default 16.0 - More is better but slower)
    float Quality = 4.0; // BLUR QUALITY (Default 4.0 - More is better but slower)
    float Size = 32; // BLUR SIZE (Radius)
    // GAUSSIAN BLUR SETTINGS }}}
   
    vec2 Radius = Size/iResolution.xy;
    
    vec2 uv = varying_texPos;
    vec4 Color = texture(diffuse, uv);
    
    for( float d=0.0; d<Pi; d+=Pi/Directions)
    {
		for(float i=1.0/Quality; i<=1.0; i+=1.0/Quality)
        {
			Color += texture( diffuse, uv+vec2(cos(d),sin(d))*Radius*i);		
        }
    }
    
    Color /= Quality * Directions - 15.0;
    Color = vec4(252.0f/255.0f,1.0f,75.0f/255.0f,Color.a * 0.5f) ;
    out_color =  Color;
}
";

			/*
            FragmentProgram = @"
                #version 330
                uniform sampler2D diffuse;
                in vec2 varying_texPos;
                out vec4 out_color;
                void main(){
	                vec4 texColor=texture(diffuse,varying_texPos);
	                vec4 color=vec4(252.0f/255.0f,1.0f,75.0f/255.0f,texColor.a * 0.5);
	                out_color=color;
                }
                ";
            */
		}
	}
}
