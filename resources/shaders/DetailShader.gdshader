shader_type spatial;
render_mode depth_prepass_alpha;

uniform sampler2D grass_tex : source_color, filter_nearest;
uniform vec3 grass_color : source_color;
uniform float tint_strength = 1.0; // how much grass inherits ground colour

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
	
	NORMAL = vec3(0.0, 0.0, 1.0);
}

void fragment() {
    vec4 tex = texture(grass_tex, UV);

    // Early out for transparent pixels (helps overdraw a bit)
    if (tex.a < 0.01) {
        discard;
    }

    // Per-instance colour (set from ChunkTileMeshData.CustomColors)
    //vec3 ground_color = INSTANCE_CUSTOM.rgb;

    // Blend grass texture toward ground colour
    vec3 final_color = mix(tex.rgb, grass_color, 1.0);

    ALBEDO = final_color;
    ALPHA  = tex.a;

    // Keep grass matte
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
}