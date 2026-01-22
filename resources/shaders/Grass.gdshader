shader_type spatial;

uniform vec4 grass_color : source_color;
uniform sampler2D grass_alpha : source_color;

void vertex() {
	// Billboard Mode: Y-Billboard
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
			vec4(normalize(cross(vec3(0.0, 1.0, 0.0), MAIN_CAM_INV_VIEW_MATRIX[2].xyz)), 0.0),
			vec4(0.0, 1.0, 0.0, 0.0),
			vec4(normalize(cross(MAIN_CAM_INV_VIEW_MATRIX[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0),
			MODEL_MATRIX[3]);

	// Billboard Keep Scale: Enabled
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
			vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
			vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
			vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
			vec4(0.0, 0.0, 0.0, 1.0));
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	ALBEDO = grass_color.rgb;
	ALPHA = texture(grass_alpha, UV).r;
	ALPHA_SCISSOR_THRESHOLD = 0.75;
	
	SPECULAR = 0.0;
	ROUGHNESS = 0.0;
}

//void light() {
	//// Cel shaded diffuse lighting
	//float light = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	//light = ceil(light * 5.0) / 5.0;
	//DIFFUSE_LIGHT += light * (LIGHT_COLOR / PI) * ATTENUATION;
//
	//// Rim Lighting
	//float rim = 1.0 - dot(NORMAL, VIEW);
	//rim *= light;
	//rim = round(rim * 5.0) / 5.0;
	//rim = rim * rim;
	//DIFFUSE_LIGHT += rim * ATTENUATION * LIGHT_COLOR / PI;
	//
//}
