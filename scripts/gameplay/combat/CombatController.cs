using System.IO;
using Godot;

public partial class CombatController : Node
{
	public const float ChargeRequiredTime = 0.5f;

	public bool IsCharging { get; private set; }
	public bool IsPlayingChargedVisuals { get; private set; }
	public bool IsDoingChargedAttack { get; private set; }
	public bool CanSwing => _cooldown <= 0f;
	public bool CanChainCombo => _comboChainTimer <= 0f;
	public QueuedAttackType QueuedAttack { get; private set; } = QueuedAttackType.None;
	public int ComboIndex { get; private set; }
	private const float ComboResetTime = 0.6f;

	private float _comboTimer;
	private float _comboChainTimer;
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

		if (_comboTimer > 0f)
		{
			_comboTimer -= dt;
			if (_comboTimer <= 0f)
			{
				ComboIndex = 0;
				QueuedAttack = QueuedAttackType.None;
			}
		}

		if (_comboChainTimer > 0f) _comboChainTimer -= dt;
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

	public void StartComboChainDelay(ToolItem tool)
	{
		_comboChainTimer = tool.ComboChainSeconds;
	}

	public void QueueLightAttack()
	{
		QueuedAttack = QueuedAttackType.Light;
	}

	public void QueueChargedAttack()
	{
		QueuedAttack = QueuedAttackType.Charged;
	}

	public bool TryConsumeQueuedAttack(out QueuedAttackType attackType)
	{
		attackType = QueuedAttack;

		if (attackType == QueuedAttackType.None || !CanChainCombo)
			return false;

		QueuedAttack = QueuedAttackType.None;
		return true;
	}

	public int ConsumeComboIndex(ToolItem tool)
	{
		var index = ComboIndex;
		var comboLength = CombatUtils.GetComboLength(tool.ToolType);

		ComboIndex++;

		if (ComboIndex >= comboLength)
			ComboIndex = 0;

		_comboTimer = ComboResetTime;

		return index;
	}

	public void ResetCombo()
	{
		ComboIndex = 0;
		QueuedAttack = QueuedAttackType.None;
		_comboTimer = 0f;
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

		IsDoingChargedAttack = true;
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
		IsDoingChargedAttack = false;
	}
}