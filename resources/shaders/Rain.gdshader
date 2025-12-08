shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec4 color : source_color = vec4(1.0, 1.0, 1.0, 0.7);

void fragment() {
    ALBEDO = color.rgb;
    ALPHA = color.a;
}