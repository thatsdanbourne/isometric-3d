shader_type spatial;
render_mode specular_disabled, cull_disabled;

global uniform float wind_scale;
global uniform float wind_speed;
global uniform float wind_strength;
global uniform vec3 wind_direction;
global uniform sampler2D wind_noise : filter_linear_mipmap;

uniform vec4 color : source_color;
uniform float blade_bend_amount = 0.08;
uniform float height_power = 1.5;
uniform float light_steps = 5.0;

varying vec4 view_pos;

void vertex() {
	vec4 world_pos = MODEL_MATRIX * vec4(VERTEX, 1.0);
	view_pos = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);

	float speed = TIME * wind_speed;

	float noise = textureLod(
		wind_noise,
		(world_pos.xz * wind_scale) - (speed * wind_direction.xz),
		1.0
	).r;

	noise = noise * 2.0 - 1.0;

	// Assumes grass base is near local Y = 0 and tip is higher.
	float height_mask = clamp(VERTEX.y, 0.0, 1.0);
	height_mask = pow(height_mask, height_power);

	vec3 wind_dir = normalize(vec3(wind_direction.x, 0.0, wind_direction.z));

	VERTEX.xz += wind_dir.xz * noise * wind_strength * blade_bend_amount * height_mask;
}

void fragment() {
	ALBEDO = color.rgb;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}

void light() {
	float light = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	light = ceil(light * light_steps) / light_steps;
	DIFFUSE_LIGHT += light * (LIGHT_COLOR / PI) * ATTENUATION;
}