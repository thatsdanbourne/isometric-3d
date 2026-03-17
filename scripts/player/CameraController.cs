using Godot;

public partial class CameraController : Node3D
{
	public Player Player;

	// ======================
	// Follow settings
	// ======================
	private float _followResponsiveness = 8.0f;

	private float _deadzoneRadius = 7.0f;
	private float _softDeadzoneExtra = 3.0f;

	private float _maxVelocity = 30.0f;
	private float _acceleration = 14.0f;
	private float _deceleration = 20.0f;

	private Vector3 _cameraOffset = new(0, 0, 65);

	private Vector3 _camVelocity = Vector3.Zero;

	// ======================
	// Zoom settings
	// ======================
	private float _targetZoom = 30.0f;
	private float _zoomSpeed = 30f;
	private float _minZoom = 15.0f;
	private float _maxZoom = 90.0f;
	private float _zoomSmoothness = 10.0f;

	// ======================
	// Shake settings
	// ======================
	private float _shakeTime;
	private float _shakeIntensity;

	private Camera3D _camera;

	public override void _Ready()
	{
		_camera = GetNode<Camera3D>("Camera3D");
		_camera.Position = _cameraOffset;

		if (Player != null)
			GlobalPosition = new Vector3(
				Player.GlobalPosition.X,
				GlobalPosition.Y,
				Player.GlobalPosition.Z
			);
	}

	public override void _Process(double delta)
	{
		var d = (float)delta;
		UpdateFollow(d);
		UpdateZoom(d);
		UpdateShake(d);
	}

	private void UpdateFollow(float delta)
	{
		var camPos = GlobalPosition;
		var playerPos = Player.GlobalPosition;

		var diff = new Vector3(playerPos.X, camPos.Y, playerPos.Z) - camPos;
		var dist = diff.Length();

		var inner = _deadzoneRadius;
		var outer = _deadzoneRadius + _softDeadzoneExtra;

		Vector3 desiredVelocity;

		if (dist < inner)
		{
			desiredVelocity = Vector3.Zero;
		}
		else if (dist < outer)
		{
			var t = (dist - inner) / (outer - inner);
			var speed = Mathf.Lerp(0f, _maxVelocity * 0.5f, t);
			desiredVelocity = diff.Normalized() * speed;
		}
		else
		{
			GlobalPosition = new Vector3(
				playerPos.X,
				GlobalPosition.Y,
				playerPos.Z
			);

			return;
		}

		var accel = desiredVelocity.Length() > _camVelocity.Length()
			? _acceleration
			: _deceleration;

		_camVelocity = _camVelocity.MoveToward(desiredVelocity, accel * delta);
		GlobalPosition += _camVelocity * delta;

		if (_camVelocity.Length() < 0.02f)
			_camVelocity = Vector3.Zero;
	}

	private void UpdateZoom(float delta)
	{
		if (Input.IsActionPressed("zoom_in"))
			_targetZoom -= _zoomSpeed * delta;
		else if (Input.IsActionPressed("zoom_out"))
			_targetZoom += _zoomSpeed * delta;

		_targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);

		if (Mathf.Abs(_camera.Size - _targetZoom) < 0.001f)
		{
			_camera.Size = _targetZoom;
			return;
		}

		var t = 1f - Mathf.Exp(-_zoomSmoothness * delta);
		_camera.Size = Mathf.Lerp(_camera.Size, _targetZoom, t);
	}

	public void Shake(float duration, float intensity)
	{
		_shakeTime = duration;
		_shakeIntensity = intensity;
	}

	private void UpdateShake(float delta)
	{
		if (_shakeTime > 0f)
		{
			_shakeTime -= delta;
			var currentIntensity = _shakeIntensity * (_shakeTime / _shakeIntensity);

			var x = (float)GD.RandRange(-currentIntensity, currentIntensity);
			var y = (float)GD.RandRange(-currentIntensity, currentIntensity);

			_camera.Position = _cameraOffset + new Vector3(x, y, 0);
		}
		else
		{
			_camera.Position = _cameraOffset;
		}
	}
}