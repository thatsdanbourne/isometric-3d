using Godot;

public partial class CameraController : Node3D
{
    public Player Player;

    // ======================
    // Follow settings
    // ======================
    private float followResponsiveness = 8.0f;

    private float deadzoneRadius = 7.0f;
    private float softDeadzoneExtra = 3.0f;

    private float maxVelocity = 30.0f;
    private float acceleration = 14.0f;
    private float deceleration = 20.0f;

    private Vector3 cameraOffset = new(0, 0, 65);

    private Vector3 camVelocity = Vector3.Zero;

    // ======================
    // Zoom settings
    // ======================
    private float targetZoom = 30.0f;
    private float zoomSpeed = 30f;
    private float minZoom = 15.0f;
    private float maxZoom = 90.0f;
    private float zoomSmoothness = 10.0f;

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
    private float texelSize = 0.0f;

    private Godot.Collections.Array<Node> pickups;
    private Vector3[] preSnapPos;

    private Transform3D snapSpace;
    private Vector3 prevRotation;

    private Camera3D camera;

    public override void _Ready()
    {
        camera = GetNode<Camera3D>("Camera3D");
        camera.Position = cameraOffset;

        if (Player != null)
            GlobalPosition = new Vector3(
                Player.GlobalPosition.X,
                GlobalPosition.Y,
                Player.GlobalPosition.Z
            );

        prevRotation = GlobalRotation;
        snapSpace = camera.GlobalTransform;

        // RenderingServer.FramePostDraw += SnapRevert;
    }

    public override void _Process(double delta)
    {
        var d = (float)delta;
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
        var camPos = GlobalPosition;
        var playerPos = Player.GlobalPosition;

        var diff = new Vector3(playerPos.X, camPos.Y, playerPos.Z) - camPos;
        var dist = diff.Length();

        var inner = deadzoneRadius;
        var outer = deadzoneRadius + softDeadzoneExtra;

        Vector3 desiredVelocity;

        if (dist < inner)
        {
            desiredVelocity = Vector3.Zero;
        }
        else if (dist < outer)
        {
            var t = (dist - inner) / (outer - inner);
            var speed = Mathf.Lerp(0f, maxVelocity * 0.5f, t);
            desiredVelocity = diff.Normalized() * speed;
        }
        else
        {
            desiredVelocity = diff.Normalized() * maxVelocity;
        }

        var accel = desiredVelocity.Length() > camVelocity.Length()
            ? acceleration
            : deceleration;

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
            targetZoom -= zoomSpeed * delta;
        else if (Input.IsActionPressed("zoom_out"))
            targetZoom += zoomSpeed * delta;

        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

        if (Mathf.Abs(camera.Size - targetZoom) < 0.001f)
        {
            camera.Size = targetZoom;
            return;
        }

        var t = 1f - Mathf.Exp(-zoomSmoothness * delta);
        camera.Size = Mathf.Lerp(camera.Size, targetZoom, t);
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
            var currentIntensity = shakeIntensity * (shakeTime / shakeIntensity);

            var x = (float)GD.RandRange(-currentIntensity, currentIntensity);
            var y = (float)GD.RandRange(-currentIntensity, currentIntensity);

            camera.Position = cameraOffset + new Vector3(x, y, 0);
        }
        else
        {
            camera.Position = cameraOffset;
        }
    }

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

        texelSize = camera.Size / (camera.GetViewport() as SubViewport).Size.Y;
        var SnapSpacePosition = snapSpace.AffineInverse() * camera.GlobalPosition;
        var SnappedSpaceSnapPosition = SnapSpacePosition.Snapped(Vector3.One * texelSize);
        var SnapError = SnappedSpaceSnapPosition - SnapSpacePosition;

        // Apply render offset ONLY
        camera.HOffset = SnapError.X;
        camera.VOffset = SnapError.Y;
        TexelError = new Vector2(SnapError.X, -SnapError.Y) / texelSize;
        // SnapPickups();
    }

    private void SnapPickups()
    {
        pickups = GetTree().GetNodesInGroup("item_pickups");
        preSnapPos = new Vector3[pickups.Count];

        for (var i = 0; i < pickups.Count; i++)
        {
            var node = pickups[i] as Node3D;
            var pos = node.GlobalPosition;
            preSnapPos[i] = pos;
            var snapSpacePos = pos * snapSpace;
            var snapped = snapSpacePos.Snapped(new Vector3(texelSize, texelSize, 0.0f));
            node.GlobalPosition = snapSpace * snapped;
        }
    }

    private void SnapRevert()
    {
        for (var i = 0; i < pickups.Count; i++)
        {
            var node = pickups[i] as Node3D;
            node.GlobalPosition = preSnapPos[i];
        }

        pickups.Clear();
    }
}