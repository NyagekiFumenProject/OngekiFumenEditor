using OngekiFumenEditor.Kernel.Graphics.Base;

namespace OngekiFumenEditor.Kernel.Graphics.Drawing.DefaultDrawingImpl.LineDrawing
{
	internal class InstancedLineShader : Shader
	{
		public InstancedLineShader()
		{
			VertexProgram = @"
                    #version 330
                  layout(location = 0) in vec3 quad_pos;
                  layout(location = 1) in vec4 line_pos_width_a;
                  layout(location = 2) in vec4 line_col_a;
                  layout(location = 3) in vec4 line_pos_width_b;
                  layout(location = 4) in vec4 line_col_b;
                  layout(location = 5) in float dashSize;
                  layout(location = 6) in float gapSize;
      
                  uniform mat4 u_mvp;
                  uniform vec2 u_viewport_size;
                  uniform vec2 u_aa_radius;

                  out vec4 v_col;
                  out float v_u;
                  out float v_v;
                  out float v_line_width;
                  out float v_line_length;

                  out float v_dashSize;
                  out float v_gapSize;

                  flat out vec3 startPos;
                  out vec3 vertPos;

                  void main()
                  {
                    v_dashSize = dashSize;
                    v_gapSize = gapSize;

                    float u_width        = u_viewport_size[0];
                    float u_height       = u_viewport_size[1];
                    float u_aspect_ratio = u_height / u_width;

                    vec4 colors[2] = vec4[2]( line_col_a, line_col_b );
                    colors[0].a *= min( 1.0, line_pos_width_a.w );
                    colors[1].a *= min( 1.0, line_pos_width_b.w );
                    v_col = colors[ int(quad_pos.x) ];

                    vec4 clip_pos_a = u_mvp * vec4( line_pos_width_a.xyz, 1.0f );
                    vec4 clip_pos_b = u_mvp * vec4( line_pos_width_b.xyz, 1.0f );

                    vec2 ndc_pos_0 = clip_pos_a.xy / clip_pos_a.w;
                    vec2 ndc_pos_1 = clip_pos_b.xy / clip_pos_b.w;

                    vec2 line_vector          = ndc_pos_1 - ndc_pos_0;
                    vec2 viewport_line_vector = line_vector * u_viewport_size;
                    vec2 dir                  = normalize( vec2( line_vector.x, line_vector.y * u_aspect_ratio ) );

                    float extension_length = u_aa_radius.y;
                    float line_length      = length( line_vector * u_viewport_size ) + 2.0 * extension_length;
                    float line_width_a     = max( 1.0, line_pos_width_a.w ) + u_aa_radius.x;
                    float line_width_b     = max( 1.0, line_pos_width_b.w ) + u_aa_radius.x;

                    vec2 normal      = vec2( -dir.y, dir.x );
                    vec2 normal_a    = vec2( line_width_a / u_width, line_width_a / u_height ) * normal;
                    vec2 normal_b    = vec2( line_width_b / u_width, line_width_b / u_height ) * normal;
                    vec2 extension   = vec2( extension_length / u_width, extension_length / u_height ) * dir;

                    v_line_width = (1.0 - quad_pos.x) * line_width_a + quad_pos.x * line_width_b;
                    v_line_length = 0.5 * line_length;
                    v_v = (2.0 * quad_pos.x - 1.0) * v_line_length;
                    v_u = quad_pos.y * v_line_width;

                    vec2 zw_part = (1.0 - quad_pos.x) * clip_pos_a.zw + quad_pos.x * clip_pos_b.zw;
                    vec2 dir_y = quad_pos.y * ((1.0 - quad_pos.x) * normal_a + quad_pos.x * normal_b);
                    vec2 dir_x = quad_pos.x * line_vector +  (2.0 * quad_pos.x - 1.0) * extension;

                    vec4 pos = vec4( (ndc_pos_0 + dir_x + dir_y) * zw_part.y, zw_part );
                    gl_Position = pos;
                    vertPos     = pos.xyz / pos.w;
                    startPos    = vertPos;
                  }
";

			FragmentProgram = @"
                #version 330
                uniform vec2 u_aa_radius;
                uniform vec2 u_viewport_size;

              in vec4 v_col;
              in float v_u;
              in float v_v;
              in float v_line_width;
              in float v_line_length;

              in float v_gapSize;
              in float v_dashSize;

              flat in vec3 startPos;
              in vec3 vertPos;

              out vec4 frag_color;
      
              void main()
              {
                vec2  dir  = (vertPos.xy-startPos.xy) * u_viewport_size/2.0;
                float dist = length(dir);

                if (fract(dist / (v_dashSize + v_gapSize)) > v_dashSize/(v_dashSize + v_gapSize))
                    discard; 

                float au = 1.0 - smoothstep( 1.0 - ((2.0*u_aa_radius[0]) / v_line_width),  1.0, abs( v_u / v_line_width ) );
                float av = 1.0 - smoothstep( 1.0 - ((2.0*u_aa_radius[1]) / v_line_length), 1.0, abs( v_v / v_line_length ) );
                frag_color = v_col;
                frag_color.a *= min(av, au);
              }
";
		}
	}
}
