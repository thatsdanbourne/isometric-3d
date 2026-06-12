shader_type spatial;
render_mode specular_disabled, depth_prepass_alpha;

global uniform float wind_scale;
global uniform float wind_speed;
global uniform float wind_strength;
global uniform vec3 wind_direction;
global uniform sampler2D wind_noise : filter_linear_mipmap;

uniform vec4 color: source_color;
uniform sampler2D alpha : hint_default_black;
uniform float leaves_scale = 1.0;
uniform float leaf_bend_amount = 0.7;
uniform float light_steps = 5.0;

varying vec3 v_view_pos;

void vertex() {
	vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

	float speed = TIME * wind_speed;
	vec2 wind_uv = world_pos.xz * wind_scale - speed * wind_direction.xz;

	float n = textureLod(wind_noise, wind_uv, 1.0).r;
	float bend = (n * 2.0 - 1.0) * wind_strength * leaf_bend_amount;

	vec2 uv_offset = UV * 2.0 - 1.0;

	// Camera-facing basis in world space
	vec3 cam_right = MAIN_CAM_INV_VIEW_MATRIX[0].xyz;
	vec3 cam_up = MAIN_CAM_INV_VIEW_MATRIX[1].xyz;

	// Billboard offset in world space
	vec3 offset_world =
		(cam_right * uv_offset.y + cam_up * uv_offset.x) * leaves_scale;

	// Wind offset in world space
	vec3 wind_dir = normalize(vec3(wind_direction.x, 0.0, wind_direction.z));
	offset_world += wind_dir * bend * uv_offset.x;

	// Convert world-space offset back into local/object space
	vec3 offset_local = (inverse(MODEL_MATRIX) * vec4(offset_world, 0.0)).xyz;

	VERTEX += offset_local;

	v_view_pos = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    ALBEDO = color.rgb;
    ALPHA = texture(alpha, UV).r;
    ALPHA_SCISSOR_THRESHOLD = 0.5;
    ROUGHNESS = 0.0;
    SPECULAR = 0.0;
    LIGHT_VERTEX = v_view_pos;
}

void light() {
	// Cel shaded diffuse lighting
	float light = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	light = ceil(light * light_steps) / light_steps;
	DIFFUSE_LIGHT += light * (LIGHT_COLOR / PI) * ATTENUATION;

	// Rim Lighting
	float rim = clamp(1.0 - dot(NORMAL, VIEW), 0.0, 1.0);
	rim *= light;
	//rim = round(rim * light_steps) / light_steps;
	rim = rim * rim;
	DIFFUSE_LIGHT += rim * ATTENUATION * LIGHT_COLOR / PI;
}
