using Godot;

public partial class CameraController : Node3D
{
	public Player Player;
	public float FollowSpeed = 5.0f;
	public Vector2 Deadzone = new Vector2(8, 8);

	public Camera3D Camera;
	private Vector3 CameraOffset = new Vector3(0, 0, 65);

	private float shakeTime = 0f;
	private float shakeIntensity = 0f;
	private Vector3 baseOffset = Vector3.Zero;
	

	public override void _Ready()
    {
        Camera = GetNode<Camera3D>("Camera3D");
		Camera.Position = CameraOffset;
		baseOffset = Position;
    }

	public override void _PhysicsProcess(double delta)
    {
        UpdateFollow((float)delta);
		// UpdateShake((float)delta);
    }

	private void UpdateFollow(float delta)
	{
		Vector3 camPos = GlobalPosition;           // controller in world space
		Vector3 targetPos = Player.GlobalPosition; // player in world space

		// Difference in world space (x,z)
		Vector3 diff = targetPos - camPos;

		float halfW = Deadzone.X * 0.5f;
		float halfH = Deadzone.Y * 0.5f;

		Vector3 desired = camPos;

		// If player is outside deadzone on X…
		if (diff.X > halfW)
			desired.X = targetPos.X - halfW;
		else if (diff.X < -halfW)
			desired.X = targetPos.X + halfW;

		// If player is outside deadzone on Z…
		if (diff.Z > halfH)
			desired.Z = targetPos.Z - halfH;
		else if (diff.Z < -halfH)
			desired.Z = targetPos.Z + halfH;

		// Smoothly move controller only
		GlobalPosition = GlobalPosition.Lerp(desired, delta * FollowSpeed);
	}


	public void Shake(float duration, float intensity)
	{
		shakeTime = duration;
		shakeIntensity = intensity;
	}

	private void UpdateShake(float delta)
	{
		if (shakeTime > 0f)
		{
			shakeTime -= delta;

			float x = (float)GD.RandRange(-shakeIntensity, shakeIntensity);
			float y = (float)GD.RandRange(-shakeIntensity, shakeIntensity);

			Camera.Position = new Vector3(x, y, 0);
		}
		else
		{
			Camera.Position = CameraOffset;
		}
	}
}

