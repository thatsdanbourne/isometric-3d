using Godot;

public partial class CameraController : Node3D
{
    public Player Player; 

    public float FollowResponsiveness = 8.0f; 
    
    // Deadzone settings in world units
    public float DeadzoneRadius = 7.0f;
	public float SoftDeadzoneExtra = 3.0f;
    
    public float MaxVelocity = 30.0f;
    
    private Camera3D camera;
	private Vector3 camVelocity = Vector3.Zero;

	// Follow settings
    private float maxVelocity = 10.0f;
	public float Acceleration = 14.0f;
	public float Deceleration = 16.0f;

	public Vector3 CameraOffset = new Vector3(0, 0, 65);

	// Zoom settings
	public float TargetZoom = 30.0f;
	public float ZoomSpeed = 0.35f;
	public float MinZoom = 15.0f;
	public float MaxZoom = 90.0f;
	public float ZoomSmoothness = 10.0f;

	// Shake settings
    private float shakeTime = 0f;
    private float shakeIntensity = 0f;
    

    public override void _Ready()
    {
        camera = GetNode<Camera3D>("Camera3D");
        camera.Position = CameraOffset;
        if (Player != null)
        {
            GlobalPosition = new Vector3(Player.GlobalPosition.X, GlobalPosition.Y, Player.GlobalPosition.Z);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateFollow((float)delta);
        UpdateShake((float)delta);

		// Zoom input handling
		if (Input.IsActionPressed("zoom_in"))
			TargetZoom = Mathf.Max(TargetZoom - ZoomSpeed, MinZoom);
		else if (Input.IsActionPressed("zoom_out"))
			TargetZoom = Mathf.Min(TargetZoom + ZoomSpeed, MaxZoom);

		TargetZoom = Mathf.Clamp(TargetZoom, MinZoom, MaxZoom);

		camera.Size = Mathf.Lerp(camera.Size, TargetZoom, ZoomSmoothness * (float)delta);
    }

    private void UpdateFollow(float delta)
    {
        Vector3 camPos = GlobalPosition;
        Vector3 playerPos = Player.GlobalPosition;

        Vector3 diff = new Vector3(playerPos.X, camPos.Y, playerPos.Z) - camPos;
        float dist = diff.Length();

        float inner = DeadzoneRadius;
        float outer = DeadzoneRadius + SoftDeadzoneExtra;

        Vector3 desiredVelocity;

        if (dist < inner)
        {
            desiredVelocity = Vector3.Zero;
        }
        else if (dist < outer)
        {
            float t = (dist - inner) / (outer - inner);   // 0 → 1
            float speed = Mathf.Lerp(0f, MaxVelocity * 0.5f, t);
            desiredVelocity = diff.Normalized() * speed;
        }
        else
        {
            desiredVelocity = diff.Normalized() * MaxVelocity;
        }

        // Smoothly move current velocity toward the desired velocity
        float accel = desiredVelocity.Length() > camVelocity.Length() ? Acceleration : Deceleration;
        camVelocity = camVelocity.MoveToward(desiredVelocity, accel * delta);

        // Apply velocity
        GlobalPosition += camVelocity * delta;
    }

    // Shake logic (included for completeness)
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

            float currentIntensity = shakeIntensity * (shakeTime / shakeIntensity);
            
            float x = (float)GD.RandRange(-currentIntensity, currentIntensity);
            float y = (float)GD.RandRange(-currentIntensity, currentIntensity);

            camera.Position = CameraOffset + new Vector3(x, y, 0); 
        }
        else
        {
            camera.Position = CameraOffset;
        }
    }
}