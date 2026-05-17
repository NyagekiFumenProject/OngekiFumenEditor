using OngekiFumenEditor.Kernel.Graphics.OpenGL.Base;

namespace OngekiFumenEditor.Kernel.Graphics.OpenGL.Drawing.LineDrawing
{
    internal sealed class GeometryPolyLineShader : DefaultOpenGLShader
    {
        private int modelViewProjectionLocation = int.MinValue;
        private int viewportSizeLocation = int.MinValue;
        private int lineWidthLocation = int.MinValue;
        private int aaRadiusLocation = int.MinValue;

        public GeometryPolyLineShader()
        {
            VertexProgram = @"
                #version 330

                layout(location = 0) in vec2 position;
                layout(location = 1) in vec4 color;
                layout(location = 2) in vec2 dash;

                uniform mat4 u_mvp;

                out vec4 v_color;
                out vec2 v_dash;

                void main()
                {
                    gl_Position = u_mvp * vec4(position, 0.0, 1.0);
                    v_color = color;
                    v_dash = dash;
                }
                ";

            GeometryProgram = @"
                #version 330

                layout(lines_adjacency) in;
                layout(triangle_strip, max_vertices = 4) out;

                uniform vec2 u_viewport_size;
                uniform float u_line_width;
                uniform float u_aa_radius;

                in vec4 v_color[];
                in vec2 v_dash[];

                noperspective out vec2 g_segment_pos;
                flat out float g_segment_length;
                flat out vec4 g_start_color;
                flat out vec4 g_end_color;
                flat out vec2 g_dash;
                flat out vec2 g_start_cut_normal;
                flat out vec2 g_end_cut_normal;
                flat out int g_has_start_cut;
                flat out int g_has_end_cut;

                vec2 clipToScreen(vec4 clip)
                {
                    vec2 ndc = clip.xy / clip.w;
                    return (ndc * 0.5 + 0.5) * u_viewport_size;
                }

                vec4 screenToClip(vec2 screen, vec4 clip_ref)
                {
                    vec2 ndc = screen / u_viewport_size * 2.0 - 1.0;
                    return vec4(ndc * clip_ref.w, clip_ref.z, clip_ref.w);
                }

                vec2 localNormal(vec2 screen_normal, vec2 dir, vec2 normal)
                {
                    return vec2(dot(screen_normal, dir), dot(screen_normal, normal));
                }

                vec2 jointCutNormal(vec2 in_dir, vec2 out_dir, vec2 fallback_normal)
                {
                    vec2 cut_normal = in_dir + out_dir;
                    float cut_len = length(cut_normal);
                    if (cut_len <= 0.0001)
                        return fallback_normal;
                    return cut_normal / cut_len;
                }

                void emitLineVertex(
                    vec2 screen_pos,
                    vec2 segment_pos,
                    vec4 clip_ref,
                    float segment_length,
                    vec4 start_color,
                    vec4 end_color,
                    vec2 dash,
                    vec2 start_cut_normal,
                    vec2 end_cut_normal,
                    int has_start_cut,
                    int has_end_cut)
                {
                    g_segment_pos = segment_pos;
                    g_segment_length = segment_length;
                    g_start_color = start_color;
                    g_end_color = end_color;
                    g_dash = dash;
                    g_start_cut_normal = start_cut_normal;
                    g_end_cut_normal = end_cut_normal;
                    g_has_start_cut = has_start_cut;
                    g_has_end_cut = has_end_cut;
                    gl_Position = screenToClip(screen_pos, clip_ref);
                    EmitVertex();
                }

                void main()
                {
                    vec4 clip_start = gl_in[1].gl_Position;
                    vec4 clip_end = gl_in[2].gl_Position;

                    if (abs(clip_start.w) <= 0.000001 || abs(clip_end.w) <= 0.000001)
                        return;

                    vec2 screen_start = clipToScreen(clip_start);
                    vec2 screen_end = clipToScreen(clip_end);
                    vec2 screen_prev = screen_start;
                    vec2 screen_next = screen_end;
                    if (abs(gl_in[0].gl_Position.w) > 0.000001)
                        screen_prev = clipToScreen(gl_in[0].gl_Position);
                    if (abs(gl_in[3].gl_Position.w) > 0.000001)
                        screen_next = clipToScreen(gl_in[3].gl_Position);

                    vec2 delta = screen_end - screen_start;
                    float len = length(delta);

                    if (len <= 0.0001)
                        return;

                    float half_width = max(u_line_width, 0.001) * 0.5;
                    float expand = half_width + max(u_aa_radius, 0.0);
                    vec2 dir = delta / len;
                    vec2 normal = vec2(-dir.y, dir.x);

                    vec2 start_ext = screen_start - dir * expand;
                    vec2 end_ext = screen_end + dir * expand;
                    vec2 offset = normal * expand;

                    int has_start_cut = 0;
                    int has_end_cut = 0;
                    vec2 start_cut_normal = vec2(0.0);
                    vec2 end_cut_normal = vec2(0.0);

                    vec2 prev_delta = screen_start - screen_prev;
                    float prev_len = length(prev_delta);
                    if (prev_len > 0.0001)
                    {
                        vec2 prev_dir = prev_delta / prev_len;
                        start_cut_normal = localNormal(jointCutNormal(prev_dir, dir, normal), dir, normal);
                        has_start_cut = 1;
                    }

                    vec2 next_delta = screen_next - screen_end;
                    float next_len = length(next_delta);
                    if (next_len > 0.0001)
                    {
                        vec2 next_dir = next_delta / next_len;
                        end_cut_normal = localNormal(jointCutNormal(dir, next_dir, normal), dir, normal);
                        has_end_cut = 1;
                    }

                    vec4 start_color = v_color[1];
                    vec4 end_color = v_color[2];
                    vec2 dash = v_dash[1];

                    emitLineVertex(start_ext - offset, vec2(-expand, -expand), clip_start, len, start_color, end_color, dash, start_cut_normal, end_cut_normal, has_start_cut, has_end_cut);
                    emitLineVertex(start_ext + offset, vec2(-expand, expand), clip_start, len, start_color, end_color, dash, start_cut_normal, end_cut_normal, has_start_cut, has_end_cut);
                    emitLineVertex(end_ext - offset, vec2(len + expand, -expand), clip_end, len, start_color, end_color, dash, start_cut_normal, end_cut_normal, has_start_cut, has_end_cut);
                    emitLineVertex(end_ext + offset, vec2(len + expand, expand), clip_end, len, start_color, end_color, dash, start_cut_normal, end_cut_normal, has_start_cut, has_end_cut);
                    EndPrimitive();
                }
                ";

            FragmentProgram = @"
                #version 330

                uniform float u_line_width;
                uniform float u_aa_radius;

                noperspective in vec2 g_segment_pos;
                flat in float g_segment_length;
                flat in vec4 g_start_color;
                flat in vec4 g_end_color;
                flat in vec2 g_dash;
                flat in vec2 g_start_cut_normal;
                flat in vec2 g_end_cut_normal;
                flat in int g_has_start_cut;
                flat in int g_has_end_cut;

                out vec4 out_color;

                float capsuleDistance(vec2 p, float segment_length, float radius)
                {
                    float x = clamp(p.x, 0.0, segment_length);
                    return length(p - vec2(x, 0.0)) - radius;
                }

                bool isDashGap(float distance_along_segment)
                {
                    if (g_dash.x <= 0.0 || g_dash.y <= 0.0)
                        return false;

                    float pattern = g_dash.x + g_dash.y;
                    if (pattern <= 0.0)
                        return false;

                    return mod(distance_along_segment, pattern) > g_dash.x;
                }

                void main()
                {
                    if (g_has_start_cut != 0 && dot(g_segment_pos, g_start_cut_normal) <= 0.0)
                        discard;

                    vec2 end_relative_pos = vec2(g_segment_pos.x - g_segment_length, g_segment_pos.y);
                    if (g_has_end_cut != 0 && dot(end_relative_pos, g_end_cut_normal) > 0.0)
                        discard;

                    float distance_along_segment = clamp(g_segment_pos.x, 0.0, g_segment_length);
                    if (isDashGap(distance_along_segment))
                        discard;

                    float radius = max(u_line_width, 0.001) * 0.5;
                    float aa = max(u_aa_radius, 0.0001);
                    float dist = capsuleDistance(g_segment_pos, g_segment_length, radius);
                    float alpha = 1.0 - smoothstep(-aa, aa, dist);

                    if (alpha <= 0.0)
                        discard;

                    float t = g_segment_length <= 0.0001 ? 0.0 : distance_along_segment / g_segment_length;
                    vec4 color = mix(g_start_color, g_end_color, t);
                    out_color = vec4(color.rgb, color.a * alpha);
                }
                ";
        }

        public int ModelViewProjectionLocation => modelViewProjectionLocation == int.MinValue ? modelViewProjectionLocation = GetUniformLocation("u_mvp") : modelViewProjectionLocation;

        public int ViewportSizeLocation => viewportSizeLocation == int.MinValue ? viewportSizeLocation = GetUniformLocation("u_viewport_size") : viewportSizeLocation;

        public int LineWidthLocation => lineWidthLocation == int.MinValue ? lineWidthLocation = GetUniformLocation("u_line_width") : lineWidthLocation;

        public int AaRadiusLocation => aaRadiusLocation == int.MinValue ? aaRadiusLocation = GetUniformLocation("u_aa_radius") : aaRadiusLocation;
    }
}
