shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform float radius : hint_range(0.0, 10.0) = 4.0;
uniform float quality : hint_range(1.0, 3.0) = 2.0;

void fragment()
{
    vec2 uv = UV;
    vec2 px = SCREEN_PIXEL_SIZE * radius * 0.5;

    vec4 col = vec4(0.0);

    // Gaussian weights
    float w0 = 0.227027;
    float w1 = 0.316216;
    float w2 = 0.070270;

    col += texture(SCREEN_TEXTURE, uv) * w0;

    col += texture(SCREEN_TEXTURE, uv + px * quality) * w1;
    col += texture(SCREEN_TEXTURE, uv - px * quality) * w1;

    col += texture(SCREEN_TEXTURE, uv + px * quality * 2.0) * w2;
    col += texture(SCREEN_TEXTURE, uv - px * quality * 2.0) * w2;

    COLOR = col;
}