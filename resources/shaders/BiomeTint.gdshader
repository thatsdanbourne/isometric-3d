shader_type canvas_item;
render_mode unshaded;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform vec3 biome_tint : source_color = vec3(1.0, 1.0, 1.0);
uniform float strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 screen_col = texture(SCREEN_TEXTURE, SCREEN_UV);
	
	vec3 tint_mix = vec3(1.0) + (biome_tint - vec3(1.0)) * strength;
	vec3 final_col = screen_col.rgb * tint_mix;
	COLOR = vec4(final_col, screen_col.a);
}
