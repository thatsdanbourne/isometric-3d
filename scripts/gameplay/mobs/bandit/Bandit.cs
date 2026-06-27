using Godot;

public partial class Bandit : Mob
{
	public float AttackStartRange;
	public float AggroRange = 15f;
	public float MoveSpeed = 4f;
	public float AttackWindup = 0.05f;

	private const float TurnSpeed = 10f;
	private const float StrafeDistancePadding = 0.45f;
	private const float StrafeDistanceTolerance = 0.35f;
	private const float StrafeLateralSpeedMultiplier = 0.55f;
	private const float StrafeRadialSpeedMultiplier = 0.5f;
	private const float ComboFollowupChance = 1f;
	private const float ComboFollowupDelay = 0.22f;
	private const float BlockChance = 0.35f;
	private const float BlockDuration = 1.2f;
	private const float RemoteAttackReturnDelay = CombatController.ComboResetTime;

	private float _cooldownMultiplier = 1.5f;
	private float _windupTimer;
	private float _remoteAttackReturnTimer;
	private bool _attackCommitted;
	private float _strafeTimer;
	private int _strafeDir = 1;
	private float _nextDecisionTimer;
	private float _blockTimer;
	private Player _targetPlayer;

	private ToolItem _equippedTool;

	private AnimationTree _animTree;
	private EntityAnimationController _animationController;
	private CombatController _combatController;

	private ToolItem _pendingAttackTool;
	private Vector3 _pendingAttackDir;

	private PhysicsRayQueryParameters3D _toolQuery;
	private const uint HittableMask = 1u << 1;

	public override void _Ready()
	{
		base._Ready();

		_animTree = GetNode<AnimationTree>("AnimationTree");
		_animationController = new EntityAnimationController();
		AddChild(_animationController);
		_animationController.Init(this, _animTree);

		_combatController = new CombatController();
		AddChild(_combatController);

		_equippedTool = ItemRegistry.GetItem("stone_sword") as ToolItem;
		AttackStartRange = _equippedTool!.HitRange;

		_toolQuery = new PhysicsRayQueryParameters3D
		{
			CollideWithAreas = false,
			CollideWithBodies = true,
			CollisionMask = HittableMask
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		var dt = (float)delta;

		if (_nextDecisionTimer > 0f) _nextDecisionTimer -= dt;

		if (StaggerTimer > 0f)
		{
			StaggerTimer -= dt;

			if (StaggerTimer <= 0f)
			{
				_animationController.ReturnToIdle();
				State = MobState.Idle;
			}
		}
	}

	public override void TickAI(double delta)
	{
		var dt = (float)delta;

		if (_combatController.Tick(dt))
			CompleteAttack();

		UpdateTarget();

		if (State == MobState.Attack && _combatController.TryConsumeQueuedAttack(out var queuedAttack))
		{
			if (queuedAttack == QueuedAttackType.Light && TryLightAttack(true))
			{
				_attackCommitted = true;
				return;
			}

			CancelAttack();
			State = MobState.Chase;
			return;
		}

		switch (State)
		{
			case MobState.Idle:
				MoveVelocity = Vector3.Zero;

				if (HasValidTargetInRange()) State = MobState.Chase;
				break;

			case MobState.Chase:
				if (!HasValidTarget())
				{
					State = MobState.Idle;
					MoveVelocity = Vector3.Zero;
					break;
				}

				var chaseDist = DistanceToTarget();

				if (chaseDist > AggroRange)
				{
					ClearTarget();
					State = MobState.Idle;
					MoveVelocity = Vector3.Zero;
					break;
				}

				MoveTowardsTarget(dt);


				if (chaseDist < AttackStartRange && _combatController.CanSwing && _nextDecisionTimer <= 0f)
				{
					if (ShouldBlock())
					{
						StartBlock();
						break;
					}

					if (GD.Randf() < 0.45f)
						StartStrafe();
					else
						StartAttack();

					_nextDecisionTimer = 0.5f;
				}

				break;

			case MobState.Strafe:
				if (!HasValidTarget())
				{
					State = MobState.Idle;
					MoveVelocity = Vector3.Zero;
					break;
				}

				if (DistanceToTarget() > AggroRange)
				{
					ClearTarget();
					State = MobState.Idle;
					MoveVelocity = Vector3.Zero;
					break;
				}

				TickStrafe(dt, _targetPlayer.GlobalPosition - GlobalPosition);
				break;

			case MobState.Attack:
				if (!HasValidTarget())
				{
					State = MobState.Idle;
					MoveVelocity = Vector3.Zero;
					break;
				}

				var attackDist = DistanceToTarget();

				FaceTarget(dt);

				if (!_attackCommitted && attackDist > _equippedTool.HitRange + 0.5f)
				{
					State = MobState.Chase;
					break;
				}

				MoveVelocity = Vector3.Zero;

				if (IsWaitingForComboFollowup())
					break;

				if (!_attackCommitted)
				{
					_windupTimer -= dt;

					if (_windupTimer <= 0)
					{
						if (TryLightAttack())
							_attackCommitted = true;
						else
							State = MobState.Chase;
					}
				}
				else
				{
					MoveVelocity = Vector3.Zero;
				}

				break;
			case MobState.Block:
				TickBlock(dt);
				break;
		}
	}

	private void TickStrafe(float dt, Vector3 toTarget)
	{
		toTarget.Y = 0f;
		var distance = toTarget.Length();
		if (distance < 0.001f)
		{
			MoveVelocity = Vector3.Zero;
			return;
		}

		_strafeTimer -= dt;
		if (_strafeTimer <= 0f)
		{
			if (distance <= _equippedTool.HitRange + 0.5f && _combatController.CanSwing)
			{
				StartAttack();
				return;
			}

			State = MobState.Chase;
			MoveVelocity = Vector3.Zero;
			_nextDecisionTimer = 0.5f;
			return;
		}

		var dirToTarget = toTarget / distance;
		var strafeDir = new Vector3(-dirToTarget.Z, 0f, dirToTarget.X) * _strafeDir;
		var desiredMove = strafeDir * MoveSpeed * StrafeLateralSpeedMultiplier;

		var rangeError = distance - StrafePreferredRange();
		if (rangeError < -StrafeDistanceTolerance)
			desiredMove -= dirToTarget * MoveSpeed * StrafeRadialSpeedMultiplier;
		else if (rangeError > StrafeDistanceTolerance)
			desiredMove += dirToTarget * MoveSpeed * StrafeRadialSpeedMultiplier;

		FaceTarget(dt);

		if (TryMoveDirection(desiredMove, desiredMove.Length(), out MoveVelocity, out _))
			return;

		MoveVelocity = Vector3.Zero;
	}

	private void TickBlock(float dt)
	{
		if (!HasValidTarget())
		{
			EndBlock();
			State = MobState.Idle;
			return;
		}

		FaceTarget(dt);
		_blockTimer -= dt;
		if (_blockTimer <= 0f)
		{
			EndBlock();
			State = MobState.Chase;
		}
	}

	protected override void TickVisuals(float dt)
	{
		if (World != null && !World.Multiplayer.IsServer())
			return;

		TickVisuals(dt, Velocity);
	}

	protected override void ApplyRemoteStateVisuals(float dt)
	{
		TickVisuals(dt, NetVelocity);
	}

	private void TickVisuals(float dt, Vector3 velocity)
	{
		var horizontalVelocity = velocity;
		horizontalVelocity.Y = 0f;

		_animationController.SetLocomotionBlend(horizontalVelocity.Length() / MoveSpeed);
		_animationController.Tick(1f - Mathf.Exp(-14f * dt));

		if (StaggerTimer > 0f)
			return;

		if (_remoteAttackReturnTimer <= 0f)
			return;

		_remoteAttackReturnTimer -= dt;
		if (_remoteAttackReturnTimer <= 0f) _animationController.ReturnToIdle();
	}

	public override void PlayRemoteAttackVisual(int comboIndex)
	{
		_remoteAttackReturnTimer = 0f;
		_animationController.PlayUseTool(_equippedTool, GlobalTransform.Basis.Z, false, comboIndex);
	}

	private void UpdateTarget()
	{
		if (HasValidTarget())
		{
			if (DistanceToTarget() <= AggroRange) return;

			ClearTarget();
		}

		_targetPlayer = AcquireNearestTarget();
	}

	private Player AcquireNearestTarget()
	{
		if (World == null) return null;

		return World.GetNearestPlayer(GlobalPosition, AggroRange);
	}

	private bool HasValidTarget()
	{
		return _targetPlayer != null && IsInstanceValid(_targetPlayer) && _targetPlayer.IsInsideTree();
	}

	private bool HasValidTargetInRange()

	{
		return HasValidTarget() && DistanceToTarget() <= AggroRange;
	}

	private void ClearTarget()
	{
		_targetPlayer = null;
	}

	private float DistanceToTarget()
	{
		if (!HasValidTarget()) return float.MaxValue;
		return GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition);
	}

	private void MoveTowardsTarget(float dt)
	{
		if (!HasValidTarget())
		{
			MoveVelocity = Vector3.Zero;
			return;
		}

		var dir = _targetPlayer.GlobalPosition - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.001f)
		{
			MoveVelocity = Vector3.Zero;
			return;
		}

		if (TryMoveDirection(dir, MoveSpeed, out MoveVelocity, out var chosenDir))
			TurnTowardsDirection(chosenDir, TurnSpeed, dt);
		else
			MoveVelocity = Vector3.Zero;
	}

	private void FaceTarget(float dt)
	{
		if (!HasValidTarget()) return;

		var dir = _targetPlayer.GlobalPosition - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.001f) return;
		TurnTowardsDirection(dir, TurnSpeed, dt);
	}

	private bool ShouldBlock()
	{
		if (!HasValidTarget()) return false;

		var targetIntent = _targetPlayer.CombatIntent;

		if (targetIntent != CombatIntent.LightAttack && targetIntent != CombatIntent.ChargedAttack) return false;

		if (_combatController.IsBlocking || _combatController.AttackInProgress) return false;

		return GD.Randf() < BlockChance;
	}

	public void StartBlock()
	{
		if (!_combatController.StartBlock())
			return;

		State = MobState.Block;
		_blockTimer = BlockDuration;
		MoveVelocity = Vector3.Zero;

		FaceTarget(0.1f);
		_animationController.PlayBlockStart(_equippedTool);
		World.Sync.BroadcastMobBlockState(this, true);
	}

	public void EndBlock()
	{
		if (!_combatController.EndBlock())
			return;

		_animationController.ReturnToIdle();
		World.Sync.BroadcastMobBlockState(this, false);
	}

	public void ApplyRemoteBlockState(bool isBlocking)
	{
		if (isBlocking)
			_animationController.PlayBlockStart(_equippedTool);
		else
			_animationController.ReturnToIdle();
	}

	private bool TryLightAttack(bool ignoreCooldown = false)
	{
		if ((!ignoreCooldown && !_combatController.CanSwing) || !HasValidTarget())
			return false;

		var swingDir = _targetPlayer.GlobalPosition - GlobalPosition;
		if (swingDir.LengthSquared() < 0.001f)
			return false;

		_combatController.StartAttack(_equippedTool);
		var comboIndex = _combatController.ConsumeComboIndex(_equippedTool);
		_animationController.PlayUseTool(_equippedTool, swingDir, false, comboIndex);

		_pendingAttackTool = _equippedTool;
		_pendingAttackDir = swingDir;

		World.Sync.BroadcastMobAttack(Uid, comboIndex);
		return true;
	}

	private void CompleteAttack()
	{
		// StartChainCooldown();
		_animationController.ReturnToIdle();
		ClearAttackState();

		if (State == MobState.Attack) State = HasValidTarget() ? MobState.Chase : MobState.Idle;
	}

	private void CancelAttack()
	{
		// StartChainCooldown();
		_combatController.CancelAttack();
		_animationController.ReturnToIdle();
		ClearAttackState();
	}

	private void StartChainCooldown()
	{
		if (!_combatController.LastChainCompleted && _combatController.LastAttackTool != null)
			_combatController.StartCooldown(_combatController.LastAttackTool, _cooldownMultiplier);
	}

	private void ClearAttackState()
	{
		_attackCommitted = false;
		_pendingAttackTool = null;
		MoveVelocity = Vector3.Zero;
	}

	private bool IsWaitingForComboFollowup()
	{
		return World != null &&
		       World.Multiplayer.IsServer() &&
		       _combatController.HasQueuedAttack &&
		       !_combatController.IsQueuedAttackReady;
	}

	private float StrafePreferredRange()
	{
		return AttackStartRange + StrafeDistancePadding;
	}

	private void StartAttack()
	{
		State = MobState.Attack;
		_windupTimer = AttackWindup;
		_attackCommitted = false;
		MoveVelocity = Vector3.Zero;
	}

	private void StartStrafe()
	{
		State = MobState.Strafe;
		_strafeTimer = (float)GD.RandRange(0.7f, 2f);
		_strafeDir = GD.Randi() % 2 == 0 ? -1 : 1;
	}

	protected override void OnStaggered(Vector3 fromDirection)
	{
		base.OnStaggered(fromDirection);

		_combatController.CancelAttack();
		_combatController.CancelCharge();
		_combatController.EndBlock();
		ClearAttackState();
		State = MobState.Staggered;
	}

	public override void ApplyRemoteStagger(Vector3 fromDirection)
	{
		base.ApplyRemoteStagger(fromDirection);

		_animationController.PlayStagger();
		_combatController.CancelAttack();
		_combatController.CancelCharge();
		_combatController.EndBlock();
		ClearAttackState();
	}

	public override void ApplyKnockback(Vector3 direction, float strength)
	{
		base.ApplyKnockback(direction, strength);

		if (State == MobState.Attack && !_attackCommitted)
		{
			State = MobState.Chase;
			_windupTimer = 0f;
			_attackCommitted = false;
		}
	}

	public override ToolHitResponse ReceiveToolHit(ToolItem tool, float damage, float knockback, float stagger,
		Vector3 fromDirection,
		Vector3 hitPoint)
	{
		var blocked = false;
		var blockingTool = _equippedTool;

		if (blockingTool != null && _combatController.IsBlocking && CombatUtils.IsBlockingHit(-GlobalTransform.Basis.Z,
			    fromDirection,
			    blockingTool.BlockStats.ArcDegrees))
		{
			damage *= 1f - blockingTool.BlockStats.DamageReduction;
			knockback *= 1f - blockingTool.BlockStats.KnockbackReduction;
			stagger *= 1f - blockingTool.BlockStats.PoiseReduction;
			blocked = true;
		}

		CurrentHealth -= damage;

		ApplyKnockback(fromDirection, knockback);

		if (!IsStaggered && ApplyPoiseDamage(stagger))
		{
			OnStaggered(fromDirection);
			World.Sync.BroadcastMobStaggered(this, fromDirection);
		}

		if (blocked)
			return ToolHitResponse.Blocked(knockback, blockingTool);

		if (!(CurrentHealth <= 0)) return ToolHitResponse.Hit(knockback);

		Die();
		return ToolHitResponse.Destroyed(knockback);
	}

	// animation callbacks
	public void OnAttackHoldFrame()
	{
		if (World != null && !World.Multiplayer.IsServer())
		{
			_remoteAttackReturnTimer = RemoteAttackReturnDelay;
			return;
		}

		if (State != MobState.Attack || !_attackCommitted)
			return;

		_combatController.OpenComboWindow();
	}

	private bool ShouldQueueComboFollowup()
	{
		if (!HasValidTarget())
			return false;

		if (DistanceToTarget() > _equippedTool.HitRange + 0.5f)
			return false;

		if (_combatController.HasCompletedCombo)
			return false;

		return GD.Randf() < ComboFollowupChance;
	}

	public void Anim_AttackHitFrame()
	{
		if (!_combatController.AttackInProgress)
			return;

		if (_pendingAttackTool == null)
			return;

		_combatController.OpenComboQueueWindow();
		World.ResolveEnemyMeleeAttack(this, _pendingAttackTool, _pendingAttackDir, _toolQuery);

		if (ShouldQueueComboFollowup())
			_combatController.QueueLightAttack(ComboFollowupDelay);
	}

	public void Anim_AttackSwingFrame()
	{
		var key = _equippedTool.ToolType is "axe" or "sword" ? "swing_blade_small" : "swing_fist";
		AudioManager.Instance.PlayVariantAt(key, GlobalPosition, AudioManager.BusTools, 0.2f);
	}
}
