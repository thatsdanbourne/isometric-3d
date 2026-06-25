using Godot;

public partial class CombatController : Node
{
	public const float ChargeRequiredTime = 0.5f;

	public bool IsCharged { get; private set; }
	public bool CanSwing => _cooldown <= 0f;
	public QueuedAttackType QueuedAttack { get; private set; } = QueuedAttackType.None;
	public bool HasQueuedAttack => QueuedAttack != QueuedAttackType.None;
	public bool IsQueuedAttackReady => HasQueuedAttack && _queuedAttackDelay <= 0f;
	public int ComboIndex { get; private set; }
	public const float ComboResetTime = 0.35f;
	public bool IsComboWindowOpen { get; private set; }
	public bool AttackInProgress { get; private set; }
	public bool IsBlocking { get; private set; }

	private float _chargeTimer;

	private float _cooldown;
	private float _comboWindowTimer;
	private float _queuedAttackDelay;

	private bool _isChargeBuffering;

	public bool Tick(float dt)
	{
		var shouldReturnToIdle = false;

		if (_cooldown > 0f)
			_cooldown -= dt;

		if (_queuedAttackDelay > 0f)
			_queuedAttackDelay -= dt;

		if (_comboWindowTimer > 0f)
		{
			_comboWindowTimer -= dt;

			if (_comboWindowTimer <= 0f)
			{
				IsComboWindowOpen = false;

				if (!AttackInProgress && !IsBlocking)
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

	public void StartCooldown(ToolItem tool, float multiplier = 1f)
	{
		_cooldown = tool.CooldownSeconds * multiplier;
	}

	public void StartAttack()
	{
		AttackInProgress = true;
	}

	public void EndAttack()
	{
		AttackInProgress = false;
	}

	public void CancelAttack()
	{
		AttackInProgress = false;
		QueuedAttack = QueuedAttackType.None;
		_queuedAttackDelay = 0f;
		IsComboWindowOpen = false;
	}

	public void QueueLightAttack(float delay = 0f)
	{
		if (!IsComboWindowOpen) return;
		QueuedAttack = QueuedAttackType.Light;
		_queuedAttackDelay = Mathf.Max(delay, 0f);
	}

	public bool TryConsumeQueuedAttack(out QueuedAttackType attackType)
	{
		attackType = QueuedAttack;

		if (attackType == QueuedAttackType.None || !IsComboWindowOpen || _queuedAttackDelay > 0f)
			return false;

		QueuedAttack = QueuedAttackType.None;
		_queuedAttackDelay = 0f;
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
		_queuedAttackDelay = 0f;
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
			CancelCharge();
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
		EndAttack();
		IsCharged = false;
		_isChargeBuffering = false;
		_chargeTimer = 0f;
	}

	public void OpenComboWindow()
	{
		EndAttack();
		IsComboWindowOpen = true;
		_comboWindowTimer = ComboResetTime;
	}

	public bool StartBlock()
	{
		if (AttackInProgress || IsCharged || _isChargeBuffering || IsBlocking)
			return false;

		ResetCombo();
		IsBlocking = true;
		return true;
	}

	public bool EndBlock()
	{
		if (IsBlocking)
		{
			IsBlocking = false;
			return true;
		}

		return false;
	}
}