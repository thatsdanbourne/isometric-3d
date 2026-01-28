using Godot;

public partial class CameraController : Node3D
{
    public Player Player;

    // ======================
    // Follow settings
    // ======================
    public float FollowResponsiveness = 8.0f;

    public float DeadzoneRadius = 7.0f;
    public float SoftDeadzoneExtra = 3.0f;

    public float MaxVelocity = 30.0f;
    public float Acceleration = 14.0f;
    public float Deceleration = 20.0f;

    public Vector3 CameraOffset = new Vector3(0, 0, 65);

    private Vector3 camVelocity = Vector3.Zero;

    // ======================
    // Zoom settings
    // ======================
    public float TargetZoom = 30.0f;
    public float ZoomSpeed = 0.15f;
    public float MinZoom = 15.0f;
    public float MaxZoom = 90.0f;
    public float ZoomSmoothness = 10.0f;

    // ======================
    // Shake settings
    // ======================
    private float shakeTime = 0f;
    private float shakeIntensity = 0f;

    // ======================
    // Pixel snap settings
    // ======================
    [Export] public bool EnablePixelSnap = true;
    public Vector2 TexelError = Vector2.Zero;

    private Transform3D snapSpace;
    private Vector3 prevRotation;

    public Camera3D camera;

    public override void _Ready()
    {
        camera = GetNode<Camera3D>("Camera3D");
        camera.Position = CameraOffset;

        if (Player != null)
        {
            GlobalPosition = new Vector3(
                Player.GlobalPosition.X,
                GlobalPosition.Y,
                Player.GlobalPosition.Z
            );
        }

        prevRotation = GlobalRotation;
        snapSpace = camera.GlobalTransform;
    }

    public override void _Process(double delta)
    {
        float d = (float)delta;
        UpdateFollow(d);
        ApplyPixelSnap();
        UpdateZoom(d);
        UpdateShake(d);
    }

    // ======================
    // FOLLOW
    // ======================
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
            float t = (dist - inner) / (outer - inner);
            float speed = Mathf.Lerp(0f, MaxVelocity * 0.5f, t);
            desiredVelocity = diff.Normalized() * speed;
        }
        else
        {
            desiredVelocity = diff.Normalized() * MaxVelocity;
        }

        float accel = desiredVelocity.Length() > camVelocity.Length()
            ? Acceleration
            : Deceleration;

        camVelocity = camVelocity.MoveToward(desiredVelocity, accel * delta);
        GlobalPosition += camVelocity * delta;

        if (camVelocity.Length() < 0.02f)
            camVelocity = Vector3.Zero;
    }

    // ======================
    // ZOOM
    // ======================
    private void UpdateZoom(float delta)
    {
        if (Input.IsActionPressed("zoom_in"))
            TargetZoom = Mathf.Max(TargetZoom - ZoomSpeed, MinZoom);
        else if (Input.IsActionPressed("zoom_out"))
            TargetZoom = Mathf.Min(TargetZoom + ZoomSpeed, MaxZoom);

        if (camera.Size == TargetZoom)
            return;

        TargetZoom = Mathf.Clamp(TargetZoom, MinZoom, MaxZoom);

        camera.Size = Mathf.Lerp(
            camera.Size,
            TargetZoom,
            ZoomSmoothness * delta
        );
    }

    // ======================
    // SHAKE
    // ======================
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

    // ======================
    // PIXEL SNAP (THE FIX)
    // ======================
    private void ApplyPixelSnap()
    {
        if (!EnablePixelSnap)
        {
            camera.HOffset = 0;
            camera.VOffset = 0;
            return;
        }

        // Reset snap space if camera rotates
        if (camera.GlobalRotation != prevRotation)
        {
            prevRotation = camera.GlobalRotation;
            snapSpace = camera.GlobalTransform;
        }

        float TexelSize = camera.Size / (camera.GetViewport() as SubViewport).Size.Y;
        Vector3 SnapSpacePosition = snapSpace.AffineInverse() * camera.GlobalPosition;
        Vector3 SnappedSpaceSnapPosition = SnapSpacePosition.Snapped(Vector3.One * TexelSize);
        Vector3 SnapError = SnappedSpaceSnapPosition - SnapSpacePosition;

        // Apply render offset ONLY
        camera.HOffset = SnapError.X;
        camera.VOffset = SnapError.Y;
        TexelError = new Vector2(SnapError.X, -SnapError.Y) / TexelSize;
    }
}