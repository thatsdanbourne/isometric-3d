using Godot;

public partial class CombatController : Node
{
	public const float ChargeRequiredTime = 0.25f;

	public bool IsCharged { get; private set; }
	public bool CanSwing => _cooldown <= 0f;
	public QueuedAttackType QueuedAttack { get; private set; } = QueuedAttackType.None;
	public int ComboIndex { get; private set; }
	public const float ComboResetTime = 0.35f;
	public bool IsComboWindowOpen { get; private set; }
	public bool AttackInProgress { get; private set; }

	private float _chargeTimer;

	private float _cooldown;
	private float _comboWindowTimer;

	private bool _isChargeBuffering;

	public bool Tick(float dt)
	{
		var shouldReturnToIdle = false;

		if (_cooldown > 0f)
			_cooldown -= dt;

		if (_comboWindowTimer > 0f)
		{
			_comboWindowTimer -= dt;

			if (_comboWindowTimer <= 0f)
			{
				IsComboWindowOpen = false;

				if (!AttackInProgress)
				{
					ResetCombo();
					shouldReturnToIdle = true;
				}
			}
		}

		return shouldReturnToIdle;
	}

	public void StartChargeBuffer()
	{
		_chargeTimer = 0f;
		_isChargeBuffering = true;
	}

	public bool TickCharge(float dt)
	{
		if (!_isChargeBuffering) return false;

		_chargeTimer += dt;

		if (_chargeTimer >= ChargeRequiredTime)
		{
			_isChargeBuffering = false;
			MarkChargeReady();
			return true;
		}

		return false;
	}

	public void StartCooldown(ToolItem tool)
	{
		_cooldown = tool.CooldownSeconds;
	}

	public void StartAttack()
	{
		AttackInProgress = true;
	}

	public void QueueLightAttack()
	{
		if (!IsComboWindowOpen) return;
		QueuedAttack = QueuedAttackType.Light;
	}

	public bool TryConsumeQueuedAttack(out QueuedAttackType attackType)
	{
		attackType = QueuedAttack;

		if (attackType == QueuedAttackType.None || !IsComboWindowOpen)
			return false;

		QueuedAttack = QueuedAttackType.None;
		IsComboWindowOpen = false;
		return true;
	}

	public int ConsumeComboIndex(ToolItem tool)
	{
		var index = ComboIndex;
		var comboLength = CombatUtils.GetComboLength(tool.ToolType);

		ComboIndex++;

		if (ComboIndex >= comboLength)
			ComboIndex = 0;

		return index;
	}

	public void ResetCombo()
	{
		ComboIndex = 0;
		QueuedAttack = QueuedAttackType.None;
	}

	public void MarkChargeReady()
	{
		IsCharged = true;
	}

	public bool ReleaseCharge(out bool isCharged)
	{
		_isChargeBuffering = false;

		if (!IsCharged)
		{
			isCharged = false;
			return false;
		}

		isCharged = true;
		IsCharged = false;
		_chargeTimer = 0f;
		return true;
	}

	public void CancelCharge()
	{
		IsCharged = false;
		_isChargeBuffering = false;
		_chargeTimer = 0f;
	}

	public void OpenComboWindow()
	{
		AttackInProgress = false;
		IsComboWindowOpen = true;
		_comboWindowTimer = ComboResetTime;
	}
}
