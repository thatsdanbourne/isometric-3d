using Godot;

public partial class EntityMotor : Node
{
	public float Acceleration { get; set; } = 20f;
	public float Deceleration { get; set; } = 28f;
	public Vector3 MovementVelocity { get; private set; }

	public float Gravity { get; set; } = 24f;
	public float KnockbackDecay { get; set; } = 14f;
	public float KnockbackResistance { get; set; } = 1f;

	public Vector3 KnockbackVelocity { get; private set; }
	public Vector3 LungeVelocity { get; private set; }

	public bool FootstepsEnabled { get; set; } = true;
	public float FootstepDistance { get; set; } = 1.8f;
	public float FootstepVolume { get; set; } = 0.2f;

	private float _footstepDistanceAccum;
	private World _world;

	private CharacterBody3D _body;
	private float _verticalVelocity;

	public void Init(CharacterBody3D body, World world)
	{
		_body = body;
		_world = world;
	}

	public void ApplyKnockback(Vector3 direction, float strength)
	{
		direction.Y = 0;

		if (direction.LengthSquared() < 0.001f)
			return;

		var desired = direction.Normalized() * (strength / KnockbackResistance);

		if (KnockbackVelocity.LengthSquared() < desired.LengthSquared())
			KnockbackVelocity = desired;
	}

	public void StartLunge(Vector3 direction, float distance, float duration)
	{
		direction.Y = 0f;

		if (direction.LengthSquared() < 0.001f || duration <= 0f)
			return;

		LungeVelocity = direction.Normalized() * (distance / duration);
	}

	public void Update(float dt, Vector3 targetMoveVelocity)
	{
		targetMoveVelocity.Y = 0;

		var rate = targetMoveVelocity.LengthSquared() > 0.001f ? Acceleration : Deceleration;

		MovementVelocity = MovementVelocity.MoveToward(targetMoveVelocity, rate * dt);
		KnockbackVelocity = KnockbackVelocity.MoveToward(Vector3.Zero, KnockbackDecay * dt);
		LungeVelocity = LungeVelocity.MoveToward(Vector3.Zero, KnockbackDecay * dt);

		ApplyGravity(dt);

		var movementSuppressed = KnockbackVelocity.LengthSquared() > 0.25f;

		if (movementSuppressed)
			targetMoveVelocity = Vector3.Zero;

		_body.Velocity = MovementVelocity + KnockbackVelocity + LungeVelocity;
		_body.Velocity = new Vector3(_body.Velocity.X, _verticalVelocity, _body.Velocity.Z);

		UpdateFootsteps(dt);
	}

	private void ApplyGravity(float dt)
	{
		const float groundY = 0f;

		if (_body.GlobalPosition.Y > groundY + 0.02f)
		{
			_verticalVelocity -= Gravity * dt;
			return;
		}

		_verticalVelocity = 0f;

		var pos = _body.GlobalPosition;
		pos.Y = groundY;
		_body.GlobalPosition = pos;
	}

	private void UpdateFootsteps(float dt)
	{
		if (!FootstepsEnabled)
			return;

		var horizontalVelocity = _body.Velocity;
		horizontalVelocity.Y = 0;

		var speed = horizontalVelocity.Length();

		if (speed < 0.1f)
			return;

		_footstepDistanceAccum += speed * dt;

		if (_footstepDistanceAccum < FootstepDistance)
			return;

		_footstepDistanceAccum = 0f;
		PlayFootstep();
	}

	private void PlayFootstep()
	{
		var tile = _world.GetTileAtPos(_body.GlobalPosition);
		if (tile == null)
			return;

		var key = tile.Value.Definition.Id switch
		{
			TileId.Grass => "footstep_grass",
			TileId.Sand => "footstep_sand",
			TileId.Snow => "footstep_snow",
			_ => "footstep_grass"
		};

		AudioManager.Instance.PlayVariantAt(
			key,
			_body.GlobalPosition,
			AudioManager.BusFootsteps,
			FootstepVolume
		);
	}
}