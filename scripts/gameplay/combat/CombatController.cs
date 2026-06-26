using Godot;

public partial class CombatController : Node
{
	public const float ChargeRequiredTime = 0.5f;
	public const float ComboResetTime = 0.5f;

	public bool IsCharged { get; private set; }
	public bool CanSwing => _cooldown <= 0f;
	public QueuedAttackType QueuedAttack { get; private set; } = QueuedAttackType.None;
	public bool HasQueuedAttack => QueuedAttack != QueuedAttackType.None;
	public bool IsQueuedAttackReady => HasQueuedAttack && _queuedAttackDelay <= 0f;
	public int ComboIndex { get; private set; }
	public bool IsComboWindowOpen { get; private set; }
	public bool AttackInProgress { get; private set; }
	public bool IsBlocking { get; private set; }
	public ToolItem LastAttackTool { get; private set; }
	public bool LastChainCompleted { get; private set; }
	public bool HasCompletedCombo => _hasCompletedCombo;

	private float _chargeTimer;

	private float _cooldown;
	private float _comboWindowTimer;
	private float _queuedAttackDelay;

	private bool _isChargeBuffering;
	private bool _hasCompletedCombo;

	public bool Tick(float dt)
	{
		TickTimer(ref _cooldown, dt);
		TickTimer(ref _queuedAttackDelay, dt);

		if (!IsComboWindowOpen || !TickTimer(ref _comboWindowTimer, dt))
			return false;

		CloseComboWindow();
		EndAttack();

		if (IsBlocking)
			return false;

		LastChainCompleted = _hasCompletedCombo;
		ComboIndex = 0;
		_hasCompletedCombo = false;
		ClearQueuedAttack();
		return true;
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
		if (tool == null)
			return;

		_cooldown = tool.CooldownSeconds * multiplier;
		LastAttackTool = null;
	}

	public void StartAttack(ToolItem tool = null)
	{
		LastChainCompleted = false;
		LastAttackTool = tool ?? LastAttackTool;
		AttackInProgress = true;
	}

	public void EndAttack()
	{
		AttackInProgress = false;
	}

	public void CancelAttack()
	{
		AttackInProgress = false;
		LastAttackTool = null;
		LastChainCompleted = false;
		_hasCompletedCombo = false;
		ClearQueuedAttack();
		CloseComboWindow();
	}

	public void QueueLightAttack(float delay = 0f)
	{
		if (!IsComboWindowOpen || _hasCompletedCombo) return;
		QueuedAttack = QueuedAttackType.Light;
		_queuedAttackDelay = Mathf.Max(delay, 0f);
	}

	public bool TryConsumeQueuedAttack(out QueuedAttackType attackType)
	{
		attackType = QueuedAttack;
		if (!IsQueuedAttackReady || !IsComboWindowOpen || _hasCompletedCombo)
			return false;

		ClearQueuedAttack();
		CloseComboWindow();
		return true;
	}

	public int ConsumeComboIndex(ToolItem tool)
	{
		var index = ComboIndex;
		var comboLength = CombatUtils.GetComboLength(tool.ToolType);

		ComboIndex++;

		if (ComboIndex >= comboLength)
		{
			ComboIndex = 0;
			_hasCompletedCombo = true;
		}

		return index;
	}

	public void ResetCombo()
	{
		ComboIndex = 0;
		LastAttackTool = null;
		LastChainCompleted = false;
		_hasCompletedCombo = false;
		ClearQueuedAttack();
	}

	public void MarkChargeReady()
	{
		IsCharged = true;
	}

	public bool ReleaseCharge(out bool isCharged)
	{
		if (_isChargeBuffering)
		{
			_isChargeBuffering = false;
			_chargeTimer = 0f;
			isCharged = false;
			return false;
		}

		isCharged = IsCharged;
		IsCharged = false;
		_chargeTimer = 0f;
		return isCharged;
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
		IsComboWindowOpen = true;
		_comboWindowTimer = ComboResetTime;
	}

	public bool StartBlock()
	{
		if (!CanStartBlock())
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

	private static bool TickTimer(ref float timer, float dt)
	{
		if (timer <= 0f)
			return false;

		timer = Mathf.Max(timer - dt, 0f);
		return timer <= 0f;
	}

	private void ClearQueuedAttack()
	{
		QueuedAttack = QueuedAttackType.None;
		_queuedAttackDelay = 0f;
	}

	private void CloseComboWindow()
	{
		IsComboWindowOpen = false;
		_comboWindowTimer = 0f;
	}

	private bool CanStartBlock()
	{
		return !AttackInProgress && !IsCharged && !_isChargeBuffering && !IsBlocking;
	}
}