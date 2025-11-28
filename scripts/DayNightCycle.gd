extends Node

@onready var sun: DirectionalLight3D = $"../Sun"
@onready var world_environment: WorldEnvironment = $"../WorldEnvironment"
@onready var environment: Environment = world_environment.environment

var MIDNIGHT_COLOR := Color(0.1, 0.1, 0.3) 
var SUNRISE_COLOR := Color(0.8, 0.7, 0.55) 
var MIDDAY_COLOR := Color(0.9, 0.9, 0.85) 
var DUSK_COLOR := Color(0.7, 0.4, 0.3)

# --- Camera/World Constants ---
const ISOMETRIC_Y_ROTATION = deg_to_rad(-45) # The rotation of the camera around Y (45 degrees)
const ISOMETRIC_X_ROTATION = deg_to_rad(60) # Sun's max height (converted to positive for rotation)

var day_length := 300.0
var time_of_day := 0.25 # 0.0=Midnight, 0.5=Midday, 1.0=Midnight

var last_angle := 999.0
var last_stretch := 999.0


func _process(delta):
	var step = delta / day_length
	
	# Continuous 0..1 loop (0.5 -> 1.0 -> 0.0 -> 0.5)
	time_of_day = fmod((time_of_day + step), 1.0)
	
	if time_of_day > 0.1 and time_of_day < 0.8:
		AudioManager.play_ambient(preload("res://assets/audio/ambience/forest_day.wav"))
	else:
		AudioManager.play_ambient(preload("res://assets/audio/ambience/forest_night.mp3"))

	update_sun()
	update_environment()


func _physics_process(_delta: float):
	update_shadows()


func update_sun():
	var t = time_of_day 

	# 1. Yaw/Azimuth (East-West path)
	var yaw_angle = lerp(0.0, TAU, t) 
	var azimuth = yaw_angle + ISOMETRIC_Y_ROTATION 
	
	# 2. Pitch/Altitude (X-Rotation): CLAMPED FIX
	# The altitude still needs to move slightly to cast shadows at different times, 
	# but we'll keep it within a narrow, optimized range around -33 degrees.
	
	# Define a target X-rotation angle for the sun's highest point (Midday t=0.5)
	# Note: We use deg_to_rad(-33) as the center of the range.
	const MIDDAY_ALTITUDE = deg_to_rad(-33.0)
	
	# Define a slight range for movement (e.g., +/- 10 degrees)
	const ALTITUDE_RANGE = deg_to_rad(10.0)
	
	# The sine wave still drives the movement within the small range: 0 (night) to 1 (midday)
	var altitude_t = sin(t * PI) 
	
	# We move the altitude from (MIDDAY_ALTITUDE - RANGE) at night to MIDDAY_ALTITUDE at midday
	var min_altitude = MIDDAY_ALTITUDE - ALTITUDE_RANGE
	var max_altitude = MIDDAY_ALTITUDE # or slightly above
	
	var altitude = lerp(min_altitude, max_altitude, altitude_t) 

	# Apply Rotation
	sun.rotation = Vector3(
		altitude, # X-Rotation: Clamped altitude
		azimuth, # Y-Rotation: East-West position
		0.0  # Roll
	)

func update_environment():
	var t = time_of_day # 0..1
	
	# Daylight calculation: Max energy at midday (t=0.5), min energy at midnight (t=0.0/1.0).
	var daylight = sin(t * PI) 
	# Ensure there's always a minimum light contribution, even at night
	daylight = max(daylight, 0.03) 

	# --- 1. Light Color Transition ---
	var new_color: Color
	var u: float # Transition factor
	
	# The color transitions must remain the same as they were working correctly:
	if t < 0.25: # Midnight (0.0/1.0) → Morning (0.25)
		u = t / 0.25
		new_color = MIDNIGHT_COLOR.lerp(SUNRISE_COLOR, u)

	elif t < 0.5: # Morning (0.25) → Midday (0.5)
		u = (t - 0.25) / 0.25
		new_color = SUNRISE_COLOR.lerp(MIDDAY_COLOR, u)

	elif t < 0.75: # Midday (0.5) → Dusk (0.75)
		u = (t - 0.5) / 0.25
		new_color = MIDDAY_COLOR.lerp(DUSK_COLOR, u) 

	else: # Dusk (0.75) → Midnight (1.0/0.0)
		u = (t - 0.75) / 0.25
		new_color = DUSK_COLOR.lerp(MIDNIGHT_COLOR, u) 
		
	sun.light_color = new_color

	# Light Energy: Max power at midday, fades at night.
	sun.light_energy = lerp(0.2, 1.0, daylight) 
	
	# --- 2. Ambient Light FIX (Crucial for 2D Sprites) ---
	
	# FIX: Increased max ambient energy to 1.0 (or higher) to illuminate sprites fully 
	# regardless of the sun's glancing angle.
	var ambient_energy = lerp(0.8, 1.0, daylight) 
	
	# Ambient color follows the sun's color, but blended toward white for natural fill light.
	environment.ambient_light_color = new_color.lerp(Color.WHITE, 0.01) 
	environment.ambient_light_energy = ambient_energy


func update_shadows():
	var cards := get_tree().get_nodes_in_group("ShadowCasters")
	if cards.is_empty():
		return

	# 1. Compute shared sun direction
	var dir := -sun.global_transform.basis.z.normalized()

	# --- 2. Compute shadow stretch (Keep this threshold) ---
	var sun_height = abs(dir.y)
	var stretch = lerp(2.0, 0.6, sun_height)

	# Only update stretch if it changed enough (This optimization is fine)
	if abs(stretch - last_stretch) < 0.0001:
		# Check angle if stretch is not updated, in case the rotation still needs to happen
		pass # Allow the rest of the function to run for rotation even if stretch is ignored

	last_stretch = stretch

	# 3. Build a basis once using look_at (The most efficient way)
	# NOTE: No need for dummy transform, use Basis.looking_at directly.
	dir.y /= 2.0
	var card_basis: Basis = Basis.looking_at(dir * 10, Vector3.UP)

	# 4. Apply to all shadow cards (ALWAYS APPLY ROTATION)
	for c in cards:
		c.apply_shadow(card_basis, stretch)
