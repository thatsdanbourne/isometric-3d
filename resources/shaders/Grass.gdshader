shader_type spatial;
render_mode blend_mix, depth_draw_opaque;

uniform bool billboard = false;

uniform sampler2D shape_texture;
uniform sampler2D shape_atlas;
uniform bool use_atlas = false;

group_uniforms Colors;
uniform sampler2D noise_texture;
uniform sampler2D color_gradient;
uniform float random_variation : hint_range(0.0, 1.0, 0.001) = 0.002;

group_uniforms Transparency;
uniform float alpha_cut_start : hint_range(0.0, 1.0, 0.05) = 0.1;
uniform float alpha_cut_end : hint_range(0.0, 1.0, 0.05) = 0.9;
uniform int alpha_mode : hint_enum("Smooth", "Dithered", "Cut") = 0;

global uniform float wind_scale;
global uniform float wind_speed;
global uniform float wind_strength;
global uniform vec3 wind_direction;
global uniform sampler2D wind_noise : filter_linear_mipmap;

varying flat int id;
varying vec3 world_pos;

#include "util/dither.gdshaderinc"

vec2 atlas_uv(vec2 uv, int index, int size){
	int tile_count = size * size;
	int i = index % tile_count;
	float x = float(i % size);
	float y = float(i / size);
	vec2 tile_size = 1.0 / vec2(float(size));
	return uv* tile_size + (vec2(x, y) * tile_size);
}

float randomf(int index, int seed){
	float value = sin(float(index + seed)) * 0.5 + 0.5;
	return value;
}

float get_wind_noise(vec3 pos) {
	float speed = TIME * wind_speed;

	float noise = textureLod(
		wind_noise,
		(pos.xz * wind_scale) - (speed * wind_direction.xz),
		1.0
	).r;

	return noise * 2.0 - 1.0;
}

vec3 get_wind_offset(vec2 uv, vec3 pos) {
	float noise = get_wind_noise(pos);

	// Billboard grass base is usually at UV.y = 1.0 and tip at UV.y = 0.0.
	float height_mask = pow(1.0 - uv.y, 2.0);

	vec3 wind_dir = normalize(vec3(wind_direction.x, 0.0, wind_direction.z));

	return wind_dir * noise * wind_strength * 0.25 * height_mask;
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	id = INSTANCE_ID;

	vec3 wind_offset = get_wind_offset(UV, world_pos) * 2.5;
	
	if(billboard){
		mat4 billboard_matrix = mat4(
				MAIN_CAM_INV_VIEW_MATRIX[0],
				MAIN_CAM_INV_VIEW_MATRIX[1],
				MAIN_CAM_INV_VIEW_MATRIX[2],
				MODEL_MATRIX[3]);
		
		MODELVIEW_MATRIX = VIEW_MATRIX * billboard_matrix;

		VERTEX += (VIEW_MATRIX * vec4(wind_offset, 0.0)).xyz;
	} else {
		VERTEX += wind_offset;
	}
	NORMAL = vec3(0.0,1.0,0.0);
}

void fragment() {
	float shape_value = texture(shape_texture, UV).r;
	if(use_atlas){
		vec2 atlas_uv = atlas_uv(UV, id, 2);
		shape_value = texture(shape_atlas, atlas_uv).r;
	}

	float noise_value = texture(noise_texture, world_pos.xz * 0.1).r;
	if(randomf(id,1) < random_variation) noise_value += 0.4;
	vec4 color = texture(color_gradient, vec2(noise_value, 0.0));

	ALBEDO = color.rgb;

	float alpha = shape_value;
	if(alpha_mode == 0){
		ALPHA = alpha;
	} else {
		alpha = clamp((shape_value - alpha_cut_start) / (alpha_cut_end - alpha_cut_start), 0.0, 1.0);
		if(alpha_mode == 1){
			alpha = step(bayer4(FRAGCOORD.xy) + 0.01, alpha);
		}
		if(alpha < 0.1) discard;
	}

}

//void light(){
	//float ndotl = dot(LIGHT, NORMAL) * ATTENUATION;
	//DIFFUSE_LIGHT += ndotl;
//}
