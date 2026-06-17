using Godot;

public partial class CombatController : Node
{
	public const float ChargeRequiredTime = 0.5f;

	public bool IsCharging { get; private set; }
	public bool IsPlayingChargedVisuals { get; private set; }
	public bool IsDoingChargedAttack { get; private set; }
	public bool CanSwing => _cooldown <= 0f;

	private float _chargeTimer;
	private float _cooldown;

	private CharacterBody3D _owner;

	public void Init(CharacterBody3D owner)
	{
		_owner = owner;
	}

	public void Tick(float dt)
	{
		if (_cooldown > 0f)
			_cooldown -= dt;
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

	public void EndChargeRelease()
	{
		IsDoingChargedAttack = false;
		IsPlayingChargedVisuals = false;
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
}