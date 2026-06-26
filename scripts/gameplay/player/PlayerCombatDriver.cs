using System;
using Godot;

public class PlayerCombatDriver
{
	private static readonly StringName UseToolAction = "use_tool";
	private static readonly StringName BlockAction = "block";

	private Player _player;
	private World _world;
	private PlayerEquipment _equipment;
	private EntityMotor _motor;
	private EntityAnimationController _animation;
	private CombatController _combat;
	private Action _updateAimDirection;
	private Func<Vector3> _getSwingDirection;

	private ToolItem _pendingAttackTool;
	private Vector3 _pendingAttackDir;
	private bool _pendingAttackCharged;
	private string _pendingSwingSoundKey;
	private int _localCombatAnimSequence;
	private int _lastRemoteCombatAnimSequence;
	private float _remoteCombatReturnTimer;

	public CombatIntent CombatIntent { get; private set; }

	public void Init(Player player, World world, PlayerEquipment equipment, EntityMotor motor,
		EntityAnimationController animation, CombatController combat, Action updateAimDirection,
		Func<Vector3> getSwingDirection)
	{
		_player = player;
		_world = world;
		_equipment = equipment;
		_motor = motor;
		_animation = animation;
		_combat = combat;
		_updateAimDirection = updateAimDirection;
		_getSwingDirection = getSwingDirection;
	}

	public void TickLocal(float dt, bool hudOpen)
	{
		if (_combat.Tick(dt) && !_combat.IsCharged)
		{
			// StartChainCooldown();
			SyncCombatAnim(PlayerCombatAnimEvent.None, string.Empty, Vector3.Zero, 0);
			_animation.ReturnToIdle();
		}

		HandleToolUse(dt, hudOpen);
	}

	public void TickRemoteCombatVisual(float dt)
	{
		if (_remoteCombatReturnTimer <= 0f)
			return;

		_remoteCombatReturnTimer -= dt;
		if (_remoteCombatReturnTimer <= 0f)
			_animation.ReturnToIdle();
	}

	public void OnAttackHitFrame()
	{
		if (!_combat.AttackInProgress)
			return;

		if (_pendingAttackTool == null)
			return;

		RequestUseActiveTool(_pendingAttackDir, _pendingAttackCharged);
	}

	public void OnAttackHoldFrame(bool isLocal)
	{
		if (!isLocal)
		{
			_remoteCombatReturnTimer = CombatController.ComboResetTime;
			return;
		}

		_combat.OpenComboWindow();
	}

	public void OnAttackSwingFrame()
	{
		PlayToolSound(_pendingSwingSoundKey, _player.GlobalPosition);
	}

	public void RequestUseActiveTool(Vector3 aimDir, bool isCharged)
	{
		_world.Sync.RequestActiveToolUse(aimDir, isCharged);
	}

	public void PlayRemoteCombatAnim(PlayerCombatAnimEvent animEvent, ToolItem tool, Vector3 swingDir, int comboIndex,
		int sequence)
	{
		if (sequence <= _lastRemoteCombatAnimSequence)
			return;

		_lastRemoteCombatAnimSequence = sequence;
		_remoteCombatReturnTimer = 0f;
		_animation.PlayCombatAnim(animEvent, tool, swingDir, comboIndex);
	}

	public void SetCombatIntent(CombatIntent intent)
	{
		CombatIntent = intent;
	}

	public void ApplyRemoteHitEvent(float health, Vector3 hitDirection, float knockback)
	{
		_player.Health = health;
		_motor.ApplyKnockback(hitDirection, knockback);
		if (_player.IsLocal)
			_player.HUD.RefreshUI();
	}

	public void HandleLocalAttackResult(ToolHitResult result)
	{
		switch (result.Outcome)
		{
			case ToolHitOutcome.Destroyed:
				_player.CameraController?.Shake(0.3f, 0.7f);
				break;

			case ToolHitOutcome.Failed:
				_player.CameraController?.Shake(0.1f, 0.3f);
				break;
		}
	}

	public void HandleAttackResult(ToolHitResult result)
	{
		switch (result.Outcome)
		{
			case ToolHitOutcome.Hit:
			case ToolHitOutcome.Blocked:
			case ToolHitOutcome.Failed:
				PlayToolSound(result.PrimarySoundKey, result.HitPoint);
				break;

			case ToolHitOutcome.Destroyed:
				PlayToolSound(result.PrimarySoundKey, result.HitPoint);
				PlayToolSound(result.BreakSoundKey, result.HitPoint);
				break;
		}
	}

	private void HandleToolUse(float dt, bool hudOpen)
	{
		if (hudOpen)
		{
			_combat.CancelCharge();
			return;
		}

		var blockingTool = _equipment.BlockingTool;
		if (blockingTool != null && Input.IsActionPressed(BlockAction) && !_combat.IsBlocking)
			if (_combat.StartBlock())
				StartBlockVisuals(blockingTool);

		if (Input.IsActionJustReleased(BlockAction))
			EndBlockVisuals(blockingTool);

		if (Input.IsActionJustPressed(UseToolAction))
			_combat.StartChargeBuffer();

		if (_combat.TickCharge(dt))
		{
			var tool = _equipment.ActiveTool;
			_animation.PlayChargeStart(tool);
			SyncCombatAnim(PlayerCombatAnimEvent.ChargeStart, tool.Id, _getSwingDirection(), 0);
		}

		if (Input.IsActionJustReleased(UseToolAction))
		{
			_combat.ReleaseCharge(out var chargedAttack);

			if (chargedAttack)
			{
				UseActiveTool(true, true);
			}
			else if (_combat.QueuedAttack == QueuedAttackType.None)
			{
				if (_combat.CanSwing && !_combat.AttackInProgress)
					UseActiveTool(false, true);
				else
					_combat.QueueLightAttack();
			}
		}

		if (_combat.TryConsumeQueuedAttack(out var queued))
			switch (queued)
			{
				case QueuedAttackType.Light:
					UseActiveTool(false, true);
					break;
				case QueuedAttackType.Charged:
					UseActiveTool(true, true);
					break;
			}
	}

	private void UseActiveTool(bool isCharged, bool ignoreCooldown = false)
	{
		if (!ignoreCooldown && !_combat.CanSwing)
			return;

		var tool = _equipment.ActiveTool;

		_updateAimDirection();
		var swingDir = _getSwingDirection();
		if (swingDir.LengthSquared() < 0.001f)
			return;

		_combat.StartAttack(tool);

		var comboIndex = isCharged ? 0 : _combat.ConsumeComboIndex(tool);
		_animation.PlayUseTool(tool, swingDir, isCharged, comboIndex);

		if (isCharged && tool.ChargedLungeDistance > 0f)
			_motor.StartLunge(swingDir, tool.ChargedLungeDistance, tool.ChargedLungeDuration);

		_pendingAttackTool = tool;
		_pendingAttackDir = swingDir;
		_pendingAttackCharged = isCharged;
		_pendingSwingSoundKey = tool.ToolType is "axe" or "sword" ? "swing_blade_small" : "swing_fist";

		SyncCombatAnim(isCharged ? PlayerCombatAnimEvent.ChargeRelease : PlayerCombatAnimEvent.LightAttack, tool.Id,
			swingDir, comboIndex);
	}

	private void StartChainCooldown()
	{
		if (!_combat.LastChainCompleted && _combat.LastAttackTool != null)
			_combat.StartCooldown(_combat.LastAttackTool);
	}

	private void StartBlockVisuals(ToolItem blockingTool)
	{
		_animation.PlayBlockStart(blockingTool);
		SyncCombatAnim(PlayerCombatAnimEvent.BlockStart, blockingTool.Id, Vector3.Zero, 0);
		_world.Sync.SetBlocking(true);
	}

	private void EndBlockVisuals(ToolItem blockingTool)
	{
		if (!_combat.EndBlock())
			return;

		_animation.ReturnToIdle();

		if (blockingTool != null)
			SyncCombatAnim(PlayerCombatAnimEvent.BlockEnd, blockingTool.Id, Vector3.Zero, 0);

		_world.Sync.SetBlocking(false);
	}

	private void SyncCombatAnim(PlayerCombatAnimEvent animEvent, string toolId, Vector3 swingDir, int comboIndex)
	{
		if (!_player.IsLocal)
			return;

		_world.Sync.SyncCombatAnim(animEvent, toolId, swingDir, comboIndex, ++_localCombatAnimSequence);
	}

	private static void PlayToolSound(string key, Vector3 hitPoint)
	{
		if (string.IsNullOrEmpty(key))
			return;

		AudioManager.Instance.PlayVariantAt(
			key,
			hitPoint,
			AudioManager.BusTools,
			0.2f
		);
	}
}