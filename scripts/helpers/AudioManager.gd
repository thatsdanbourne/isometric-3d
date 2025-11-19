extends Node

const BUS_WORLD := "World"
const BUS_TOOLS := "Tools"
const BUS_UI := "UI"
const BUS_FOOTSTEPS := "Footsteps"
const BUS_AMBIENCE := "Ambience"
const BUS_MUSIC := "Music"

var music_player := AudioStreamPlayer.new()
var music_tracks: Array[AudioStream] = []
var next_music_time := 0.0
var music_min_delay := 60.0
var music_max_delay := 180.0

var ambient_player := AudioStreamPlayer.new()
var ambient_target := AudioStreamPlayer.new()

var fade_speed := 5.0
var current_ambient := ""

func _ready():
	_setup_ambient_player(ambient_player)
	_setup_ambient_player(ambient_target)

	add_child(ambient_player)
	add_child(ambient_target)

	music_player.bus = BUS_MUSIC
	music_player.volume_db = -6
	add_child(music_player)

	music_tracks = [
		preload("res://assets/audio/music/relativity.mp3"),
		preload("res://assets/audio/music/explorers.mp3")
	]

	_schedule_next_music()


func _process(delta: float):
	if music_tracks.is_empty(): return

	next_music_time -= delta

	if next_music_time <= 0.0:
		_play_random_music()
		_schedule_next_music()


func _setup_ambient_player(player: AudioStreamPlayer):
	player.bus = BUS_AMBIENCE
	player.volume_db = -80
	player.stream_paused = true
	player.autoplay = false
	player.process_mode = Node.PROCESS_MODE_ALWAYS


func play_at(stream: AudioStream, position: Vector3, bus: String = BUS_WORLD, pitch_range: float = 0.0, volume_offset_db: float = 0.0):
	var player := AudioStreamPlayer3D.new()
	get_tree().current_scene.add_child(player)
	player.global_position = position
	player.stream = stream
	player.bus = bus

	player.attenuation_model = AudioStreamPlayer3D.ATTENUATION_LOGARITHMIC
	player.unit_size = 1.0
	player.max_distance = 40.0
	player.attenuation_filter_db = 6
	player.attenuation_filter_cutoff_hz = 18000
	player.pitch_scale = randf_range(1.0 - pitch_range, 1.0 + pitch_range)
	player.volume_db = volume_offset_db

	player.play()
	player.finished.connect(player.queue_free)


func play_random_at(streams: Array[AudioStream], position: Vector3, bus: String = BUS_WORLD, pitch_range: float = 0.0, volume_offset_db: float = 0.0):
	if streams.is_empty():
		return
	
	var chosen = streams.pick_random()
	play_at(chosen, position, bus, pitch_range, volume_offset_db)


func play_ui(stream: AudioStream, pitch_range := 0.05, volume_offset_db := 0.0):
	var p := AudioStreamPlayer.new()
	get_tree().current_scene.add_child(p)

	p.bus = BUS_UI
	p.stream = stream
	p.pitch_scale = randf_range(1.0 - pitch_range, 1.0 + pitch_range)
	p.volume_db = volume_offset_db

	p.play()
	p.finished.connect(p.queue_free)


func play_ambient(stream: AudioStream, fade_time: float = 20):
	if not is_inside_tree():
		await ready

	if not stream: return
	if current_ambient == stream.resource_path: return

	var tmp = ambient_player
	ambient_player = ambient_target
	ambient_target = tmp

	ambient_target.stream = stream
	ambient_target.volume_db = -80
	ambient_target.stream_paused = false
	ambient_target.play()

	current_ambient = stream.resource_path
	fade_speed = fade_time

	await _fade_ambients()


func _fade_ambients():
	var out_tween = create_tween().set_ease(Tween.EASE_IN).set_trans(Tween.TRANS_SINE)
	out_tween.tween_property(ambient_player, "volume_db", -80.0, fade_speed)

	var in_tween = create_tween().set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_EXPO)
	in_tween.tween_property(ambient_target, "volume_db", 0.0, fade_speed)

	await out_tween.finished


func _schedule_next_music():
	next_music_time = randf_range(music_min_delay, music_max_delay)


func _play_random_music():
	if music_player.playing:
		return
	
	var track = music_tracks.pick_random()
	music_player.stream = track
	music_player.volume_db = -40
	music_player.play()

	var t := create_tween()
	t.tween_property(music_player, "volume_db", -6.0, 3.0).set_trans(Tween.TRANS_SINE)

	var fade_out_time := 3.0
	var track_length = track.get_length()
	var fade_start = max(track_length - fade_out_time, 0.1)

	await get_tree().create_timer(fade_start).timeout

	var t2 := create_tween()
	t2.tween_property(music_player, "volume_db", -40, fade_out_time).set_trans(Tween.TRANS_SINE)

	await t2.finished
	music_player.stop()
