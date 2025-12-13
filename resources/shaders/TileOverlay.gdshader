shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_opaque;
// blend_mix: Use alpha blending. depth_draw_opaque: only write depth for opaque parts.

// The texture you want to use for the rocks overlay
uniform sampler2D albedo_tex : source_color;
// A multiplier to tile the texture.
uniform float overlay_tiling_scale = 0.5;
// The crucial compensation factor
uniform float perspective_scale = 2.0;

void fragment() {
    // 1. Get the world position of the current pixel.
    vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

    // 2. Prepare and correct UV coordinates.
    vec2 world_uv = world_pos.xz * overlay_tiling_scale;
    world_uv.y *= perspective_scale; // PERSPECTIVE COMPENSATION

    // 3. Sample the texture.
    vec4 overlay_color = texture(albedo_tex, world_uv);

    // 4. Apply the color and alpha.
    ALBEDO = overlay_color.rgb;
    ALPHA = overlay_color.a;

    // Discard fragments that are nearly transparent to prevent drawing
    // where the rock texture is empty, ensuring the base GridMap shows through.
    if (ALPHA < 0.01) {
        discard;
    }
}