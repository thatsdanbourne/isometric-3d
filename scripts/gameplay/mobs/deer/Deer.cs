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

	public override void TickAI(double delta)
	{
		var dt = (float)delta;

		_timer -= dt;
		if (_timer <= 0f)
			PickNewDirection();

		if (TryMoveDirection(_desiredDir, MoveSpeed, out MoveVelocity, out var chosenDir))
			TurnTowardsDirection(chosenDir, TurnSpeed, dt);
		else
			MoveVelocity = Vector3.Zero;
	}

	protected override void ApplyRemoteStateVisuals(float dt)
	{
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

		var isMoving = new Vector2(NetVelocity.X, NetVelocity.Z).Length() > 0.1f;

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