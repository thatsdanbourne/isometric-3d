using Godot;

public partial class EntityMotor : Node
{
	public float Gravity { get; set; } = 24f;
	public float KnockbackDecay { get; set; } = 14f;
	public float KnockbackResistance { get; set; } = 1f;

	public Vector3 KnockbackVelocity { get; private set; }
	public Vector3 LungeVelocity { get; private set; }

	private CharacterBody3D _body;
	private float _verticalVelocity;

	public void Init(CharacterBody3D body)
	{
		_body = body;
	}

	public void ApplyKnockback(Vector3 direction, float strength)
	{
		direction.Y = 0;

		if (direction.LengthSquared() < 0.001f)
			return;

		KnockbackVelocity += direction.Normalized() * (strength / KnockbackResistance);
	}

	public void StartLunge(Vector3 direction, float distance, float duration)
	{
		direction.Y = 0f;

		if (direction.LengthSquared() < 0.001f || duration <= 0f)
			return;

		LungeVelocity = direction.Normalized() * (distance / duration);
	}

	public void Update(float dt, Vector3 moveVelocity)
	{
		KnockbackVelocity = KnockbackVelocity.MoveToward(Vector3.Zero, KnockbackDecay * dt);
		LungeVelocity = LungeVelocity.MoveToward(Vector3.Zero, KnockbackDecay * dt);

		ApplyGravity(dt);

		_body.Velocity = moveVelocity + KnockbackVelocity + LungeVelocity;
		_body.Velocity = new Vector3(_body.Velocity.X, _verticalVelocity, _body.Velocity.Z);
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
}