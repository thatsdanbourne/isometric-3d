using Godot;

public partial class Bandit : Mob
{
	public float AttackStartRange;
	public float AggroRange = 15f;
	public float MoveSpeed = 4f;
	public float AttackWindup = 0.05f;
	public float AttackRecovery = 0.35f;

	private const float TurnSpeed = 10f;
	private const float StrafeDistancePadding = 0.45f;
	private const float StrafeDistanceTolerance = 0.35f;
	private const float StrafeLateralSpeedMultiplier = 0.55f;
	private const float StrafeRadialSpeedMultiplier = 0.5f;
	private const float ComboFollowupChance = 1f;
	private const float ComboFollowupDelay = 0.22f;
	private const float RemoteAttackReturnDelay = CombatController.ComboResetTime;

	private float _cooldownMultiplier = 1.5f;
	private float _windupTimer;
	private float _recoveryTimer;
	private float _remoteAttackReturnTimer;
	private bool _attackCommitted;
	private float _strafeTimer;
	private int _strafeDir = 1;
	private float _nextDecisionTimer;
	private float _comboFollowupTimer;
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

		_equippedTool = ItemRegistry.GetItem("stone_axe") as ToolItem;
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

		_recoveryTimer -= dt;

		if (_combatController.Tick(dt) && !IsWaitingForComboFollowup())
			_animationController.ReturnToIdle();

		UpdateTarget();

		if (_comboFollowupTimer > 0f)
			_comboFollowupTimer -= dt;

		if (State == MobState.Attack &&
		    _comboFollowupTimer <= 0f &&
		    _combatController.TryConsumeQueuedAttack(out var queuedAttack) &&
		    queuedAttack == QueuedAttackType.Light)
		{
			if (TryLightAttack(true))
			{
				_attackCommitted = true;
				_recoveryTimer = AttackRecovery;
			}
			else
			{
				State = MobState.Chase;
			}
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

				if (chaseDist < AttackStartRange && _recoveryTimer <= 0f && _combatController.CanSwing &&
				    _nextDecisionTimer <= 0f)
				{
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
					FinishAttack();
					MoveVelocity = Vector3.Zero;
					break;
				}

				var attackDist = DistanceToTarget();

				FaceTarget(dt);

				if (attackDist > _equippedTool.HitRange + 0.5f)
				{
					State = MobState.Chase;
					FinishAttack();
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
						{
							_attackCommitted = true;
							_recoveryTimer = AttackRecovery;
						}
						else
						{
							State = MobState.Chase;
						}
					}
				}
				else
				{
					if (_recoveryTimer <= 0f)
					{
						if (_combatController.QueuedAttack != QueuedAttackType.None)
						{
							HoldForComboFollowup();
							break;
						}

						FinishAttack();
						State = MobState.Chase;
					}
				}

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
			if (distance <= _equippedTool.HitRange + 0.5f && _recoveryTimer <= 0f && _combatController.CanSwing)
			{
				StartAttack();
				return;
			}

			State = MobState.Chase;
			MoveVelocity = Vector3.Zero;
			_nextDecisionTimer = 0.25f;
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
		if (_remoteAttackReturnTimer <= 0f)
			_animationController.ReturnToIdle();
	}

	public override void PlayRemoteAttackVisual(int comboIndex)
	{
		_animationController.PlayUseTool(_equippedTool, GlobalTransform.Basis.Z, false, comboIndex);
		_remoteAttackReturnTimer = AttackRecovery;
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

	private bool TryLightAttack(bool ignoreCooldown = false)
	{
		if ((!ignoreCooldown && !_combatController.CanSwing) || !HasValidTarget())
			return false;

		var swingDir = _targetPlayer.GlobalPosition - GlobalPosition;
		if (swingDir.LengthSquared() < 0.001f)
			return false;

		_combatController.StartAttack();
		_combatController.StartCooldown(_equippedTool, _cooldownMultiplier);
		var comboIndex = _combatController.ConsumeComboIndex(_equippedTool);
		_animationController.PlayUseTool(_equippedTool, swingDir, false, comboIndex);

		_pendingAttackTool = _equippedTool;
		_pendingAttackDir = swingDir;

		World.Sync.BroadcastMobAttack(Uid, comboIndex);
		return true;
	}

	private void FinishAttack()
	{
		_combatController.EndAttack();
		_animationController.ReturnToIdle();
		_attackCommitted = false;
		_comboFollowupTimer = 0f;
	}

	private void HoldForComboFollowup()
	{
		_combatController.EndAttack();
		_attackCommitted = false;
		MoveVelocity = Vector3.Zero;
	}

	private bool IsWaitingForComboFollowup()
	{
		return World != null &&
		       World.Multiplayer.IsServer() &&
		       _combatController.QueuedAttack != QueuedAttackType.None &&
		       _comboFollowupTimer > 0f;
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
		_strafeTimer = (float)GD.RandRange(0.4f, 0.9f);
		_strafeDir = GD.Randi() % 2 == 0 ? -1 : 1;
	}

	protected override void OnStaggered(Vector3 fromDirection)
	{
		base.OnStaggered(fromDirection);

		_animationController.ReturnToIdle();
		_combatController.EndAttack();
		_combatController.CancelCharge();
		_comboFollowupTimer = 0f;
		State = MobState.Staggered;
	}

	public override void ApplyRemoteStagger(Vector3 fromDirection)
	{
		base.ApplyRemoteStagger(fromDirection);

		_animationController.PlayStagger();
		_combatController.CancelCharge();
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

	// animation callbacks
	public void OnAttackHoldFrame()
	{
		_animationController.HoldCurrentAttackPose();

		if (World != null && !World.Multiplayer.IsServer())
		{
			_remoteAttackReturnTimer = RemoteAttackReturnDelay;
			return;
		}

		_combatController.OpenComboWindow();
		_comboFollowupTimer = 0f;

		if (GD.Randf() < ComboFollowupChance)
		{
			_combatController.QueueLightAttack();
			_comboFollowupTimer = ComboFollowupDelay;
		}
	}

	public void Anim_AttackHitFrame()
	{
		if (!_combatController.AttackInProgress)
			return;

		if (_pendingAttackTool == null)
			return;

		World.ResolveEnemyMeleeAttack(this, _pendingAttackTool, _pendingAttackDir, _toolQuery);
	}
}
