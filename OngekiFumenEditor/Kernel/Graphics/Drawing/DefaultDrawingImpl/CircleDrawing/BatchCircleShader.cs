using OngekiFumenEditor.Kernel.Graphics.Base;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.CircleDrawing
{
	internal class BatchCircleShader : Shader
	{
		private static BatchCircleShader _shared;

		public BatchCircleShader()
		{
			VertexProgram = @"
                #version 330

                out vec2 pointPos;
                out float varying_radius;
                out vec4 varying_color;

                uniform mat4 ModelViewProjection;
                uniform vec2 uResolution;

                layout(location=0) in vec4 in_color;
                layout(location=1) in vec2 in_pos;
                layout(location=2) in float in_radius;

                void main(){
                    varying_color = in_color;
                    varying_radius = in_radius;

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

                const float threshold = 0.3;

                out vec4 out_color;

                void main(){
	                float dist = distance(pointPos, gl_FragCoord.xy);
                    if (dist > varying_radius)
                        discard;

                    float d = dist / varying_radius;
                    out_color = mix(varying_color, vec4(varying_color.rgb,0.0), step(1.0-threshold, d));
                }
                ";
		}

		public static BatchCircleShader Shared => _shared ??= _createShared();

		private static BatchCircleShader _createShared()
		{
			var shader = new BatchCircleShader();
			shader.Compile();
			GLUtility.CheckError("Create shared BatchCircleShader object failed");
			return shader;
		}
	}
}
