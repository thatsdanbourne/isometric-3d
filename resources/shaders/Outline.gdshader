shader_type spatial;
render_mode unshaded, cull_disabled;

uniform sampler2D albedo_texture : source_color, filter_nearest;
uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float outline_size : hint_range(0, 10) = 1.0;
uniform bool enabled = true;
uniform float alpha_cutoff : hint_range(0, 1) = 0.5;

void fragment() {
    if (!enabled) {
        discard;
    }

    vec4 base_tex = texture(albedo_texture, UV);

    // Don't draw over solid sprite pixels
    if (base_tex.a >= alpha_cutoff) {
        discard;
    }

    vec2 texel_size = outline_size / vec2(textureSize(albedo_texture, 0));

    float max_alpha = 0.0;

    for (float x = -1.0; x <= 1.0; x++) {
        for (float y = -1.0; y <= 1.0; y++) {
            if (x == 0.0 && y == 0.0) continue;
            max_alpha = max(
                max_alpha,
                texture(albedo_texture, UV + vec2(x, y) * texel_size).a
            );
        }
    }

    if (max_alpha >= alpha_cutoff) {
        ALBEDO = outline_color.rgb;
        ALPHA = outline_color.a;
    } else {
        discard;
    }
}