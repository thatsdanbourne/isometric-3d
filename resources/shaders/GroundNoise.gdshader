shader_type spatial;
render_mode cull_disabled;

uniform sampler2D tile_tex : source_color, filter_nearest;
uniform sampler2D noise_tex : repeat_enable, filter_linear;

uniform float noise_scale = 0.01;
uniform float noise_strength = 0.2; // overall influence
uniform int steps = 4;               // number of value bands (2–4 works best)

varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec3 base_color = texture(tile_tex, UV).rgb;

    vec2 world_uv = world_pos.xz * noise_scale;
    float n = texture(noise_tex, world_uv).r;

    // Quantise noise into steps
    float stepped = floor(n * float(steps)) / float(steps - 1);

    // Convert 0..1 → -1..+1
    float centered = stepped * 2.0 - 1.0;

    // Value-only modulation
    float value = 1.0 + centered * noise_strength;
    base_color *= value;

    ALBEDO = clamp(base_color, vec3(0.0), vec3(1.0));
}