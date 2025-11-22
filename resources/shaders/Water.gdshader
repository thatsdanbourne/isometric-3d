shader_type spatial;

render_mode blend_mix, depth_draw_opaque, cull_back;

uniform sampler2D water_tex;
uniform float speed = 0.5;
uniform float scale = 1.0;
uniform float alpha = 0.7;

void fragment() {
    vec2 uv = UV;
    uv.x += TIME * speed;
    uv *= scale;

    vec4 col = texture(water_tex, uv);

    ALBEDO = col.rgb;
    ALPHA = alpha; // semi-transparent
}