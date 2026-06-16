using Godot;

public partial class CombatController : Node
{
	public const float ChargeRequiredTime = 0.5f;

	public bool IsCharging { get; private set; }
	public bool IsPlayingChargedVisuals { get; private set; }
	public bool IsDoingChargedAttack { get; private set; }
	public bool CanSwing => _cooldown <= 0f;

	public Vector3 LungeVelocity { get; private set; }

	private float _chargeTimer;
	private float _cooldown;
	private float _lungeDecay = 14f;

	private CharacterBody3D _owner;

	public void Init(CharacterBody3D owner)
	{
		_owner = owner;
	}

	public void Tick(float dt)
	{
		if (_cooldown > 0f)
			_cooldown -= dt;

		LungeVelocity = LungeVelocity.MoveToward(Vector3.Zero, _lungeDecay * dt);
	}

	public bool TickCharge(float dt)
	{
		if (!IsCharging)
			return false;

		_chargeTimer += dt;

		if (_chargeTimer >= ChargeRequiredTime && !IsPlayingChargedVisuals)
		{
			IsPlayingChargedVisuals = true;
			return true;
		}

		return false;
	}

	public bool TickAnimationState(AnimationTree animTree)
	{
		if (!IsDoingChargedAttack)
			return false;

		var active = (bool)animTree.Get("parameters/HeavyAttackOS/active");

		if (active)
			return false;

		IsDoingChargedAttack = false;
		IsPlayingChargedVisuals = false;

		return true;
	}

	public void StartCooldown(ToolItem tool)
	{
		_cooldown = tool.CooldownSeconds;
	}

	public void StartCharge()
	{
		IsCharging = true;
		_chargeTimer = 0f;
		IsPlayingChargedVisuals = false;
	}


	public bool ReleaseCharge(out bool isCharged)
	{
		if (!IsCharging)
		{
			isCharged = false;
			return false;
		}

		isCharged = _chargeTimer >= ChargeRequiredTime;
		IsCharging = false;
		_chargeTimer = 0f;
		return true;
	}

	public void CancelCharge()
	{
		IsCharging = false;
		_chargeTimer = 0f;
		IsPlayingChargedVisuals = false;
	}

	public void StartChargedRelease()
	{
		IsDoingChargedAttack = true;
		IsPlayingChargedVisuals = true;
	}

	public void EndChargedRelease()
	{
		IsDoingChargedAttack = false;
		IsPlayingChargedVisuals = false;
	}

	public void StartLunge(Vector3 direction, float distance, float duration)
	{
		direction.Y = 0f;

		if (direction.LengthSquared() < 0.001f || duration <= 0f)
			return;

		LungeVelocity = direction.Normalized() * (distance / duration);
	}
}