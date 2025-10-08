using OngekiFumenEditor.Kernel.Graphics.OpenGL;
using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.CircleDrawing
{
    internal class BatchCircleShader : DefaultOpenGLShader
	{
		private static BatchCircleShader _shared;

		public BatchCircleShader()
		{
			VertexProgram = @"
                #version 330

                out vec2 pointPos;
                out float varying_radius;
                out vec4 varying_color;
                out float varying_lineWidth;

                uniform mat4 ModelViewProjection;
                uniform vec2 uResolution;

                layout(location=0) in vec4 in_color;
                layout(location=1) in vec2 in_pos;
                layout(location=2) in float in_radius;
                layout(location=3) in float in_lineWidth;

                void main(){
                    varying_color = in_color;
                    varying_radius = in_radius;
                    varying_lineWidth = in_lineWidth;

	                gl_Position=ModelViewProjection * vec4(in_pos,0.0,1.0);
                    gl_PointSize = 900.0;

                    vec2 ndcPos = gl_Position.xy / gl_Position.w;
                    pointPos    = uResolution * (ndcPos*0.5 + 0.5);
                }
                ";


			FragmentProgram = @"
                #version 330

                in vec2  pointPos;
                in vec4  varying_color;
                in float  varying_radius;
                in float  varying_lineWidth;

                const float uEdgeSoftness = 0.15;

                out vec4 out_color;

                void main(){
	                float dist = distance(pointPos, gl_FragCoord.xy);


                    if (dist > varying_radius)
                        discard;

                    float d = dist / varying_radius;
                    
                    if (varying_lineWidth > 0.0) {

            float inner_radius = 1.0 - varying_lineWidth / varying_radius;
            
            if (d < inner_radius) {

                discard;
            } else {

                float ring_pos = (d - inner_radius) / (1.0 - inner_radius);
                float alpha = 1.0 - smoothstep(1.0 - uEdgeSoftness, 1.0, ring_pos);
                out_color = vec4(varying_color.rgb, varying_color.a * alpha);
            }
                    }
else{
                    float alpha = 1.0 - smoothstep(1.0 - uEdgeSoftness, 1.0, d);
                    out_color = vec4(varying_color.rgb, varying_color.a * alpha);
}
                }
                ";
		}

		public static BatchCircleShader Shared => _shared ??= _createShared();

		private static BatchCircleShader _createShared()
		{
			var shader = new BatchCircleShader();
			shader.Compile();
			return shader;
		}
	}
}
