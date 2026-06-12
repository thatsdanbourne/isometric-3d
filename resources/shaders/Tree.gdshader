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

	vec3 view_offset = normalize(vec3(uv_offset.y, uv_offset.x, 0.0)) * leaves_scale;

	view_offset.x += bend * uv_offset.x;
	view_offset.y -= bend * uv_offset.y;

	VERTEX += (inverse(MODELVIEW_MATRIX) * vec4(view_offset, 0.0)).xyz;

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
    // Pre-calculate 1.0 / PI to turn division into multiplication
    const float ONE_OVER_PI = 0.31830988618;
    float inv_steps = 1.0 / light_steps;

    // Cel shaded diffuse lighting
    float dot_nl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
    // Using multiplication instead of ceil(x * steps) / steps
    float light = ceil(dot_nl * light_steps) * inv_steps;
    
    vec3 base_light = light * LIGHT_COLOR * ONE_OVER_PI * ATTENUATION;
    DIFFUSE_LIGHT += base_light;

    // Rim Lighting
    float rim = clamp(1.0 - dot(NORMAL, VIEW), 0.0, 1.0) * light;
    rim = round(rim * light_steps) * inv_steps;
    rim *= rim;
    
    DIFFUSE_LIGHT += rim * ATTENUATION * LIGHT_COLOR * ONE_OVER_PI;
}

//void light() {
	//// Cel shaded diffuse lighting
	//float light = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	//light = ceil(light * light_steps) / light_steps;
	//DIFFUSE_LIGHT += light * (LIGHT_COLOR / PI) * ATTENUATION;
//
	//// Rim Lighting
	//float rim = 1.0 - dot(NORMAL, VIEW);
	//rim *= light;
	////rim = round(rim * light_steps) / light_steps;
	//rim = rim * rim;
	//DIFFUSE_LIGHT += rim * ATTENUATION * LIGHT_COLOR / PI;
//}
