using Godot;

public partial class Deer : Mob
{
	[Export] public float MoveSpeed = 2.2f;
	[Export] public float TurnSpeed = 6.0f;

	[Export] public float WanderRadius = 6.0f;
	[Export] public float WanderIntervalMin = 1.2f;
	[Export] public float WanderIntervalMax = 2.6f;

	private AnimationPlayer _animPlayer;

	private Vector3 _desiredDir = Vector3.Zero;
	private float _timer;

	public override void _Ready()
	{
		base._Ready();

		_animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");

		PickNewDirection();
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;

		_timer -= dt;
		if (_timer <= 0f)
			PickNewDirection();

		if (_desiredDir.LengthSquared() > 0.0001f)
		{
			var forward = GlobalTransform.Basis.Z;
			var targetForward = _desiredDir.Normalized();

			forward.Y = 0;
			targetForward.Y = 0;

			if (forward.LengthSquared() > 0.0001f && targetForward.LengthSquared() > 0.0001f)
			{
				forward = forward.Normalized();
				targetForward = targetForward.Normalized();

				var angle = Mathf.Atan2(forward.Cross(targetForward).Y, forward.Dot(targetForward));
				var step = Mathf.Clamp(angle, -TurnSpeed * dt, TurnSpeed * dt);
				RotateY(step);
			}

			var moveDir = GlobalTransform.Basis.Z;
			moveDir.Y = 0;
			moveDir = moveDir.Normalized();

			Velocity = new Vector3(moveDir.X * MoveSpeed, Velocity.Y, moveDir.Z * MoveSpeed);
		}
		else
		{
			Velocity = new Vector3(0f, Velocity.Y, 0f);
		}

		MoveAndSlide();

		UpdateAnim();
	}

	private void PickNewDirection()
	{
		_timer = (float)GD.RandRange(WanderIntervalMin, WanderIntervalMax);

		var yaw = (float)GD.RandRange(-Mathf.Pi, Mathf.Pi);
		_desiredDir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));

		if (GD.Randf() < 0.2f)
			_desiredDir = Vector3.Zero;
	}

	private void UpdateAnim()
	{
		if (_animPlayer == null) return;

		var isMoving = new Vector2(Velocity.X, Velocity.Z).Length() > 0.1f;

		if (isMoving)
		{
			if (!_animPlayer.IsPlaying() || _animPlayer.CurrentAnimation != "walk")
				_animPlayer.Play("walk");
		}
		else
		{
			_animPlayer.Stop();
		}
	}
}