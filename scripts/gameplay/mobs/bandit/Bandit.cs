using Godot;

public partial class Bandit : Mob
{
	public float AttackStartRange;
	public float AggroRange = 15f;
	public float MoveSpeed = 4f;
	public float AttackWindup = 0.05f;
	public float AttackRecovery = 0.35f;

	private const float TurnSpeed = 10f;

	private float _cooldownMultiplier = 1.5f;
	private float _windupTimer;
	private float _recoveryTimer;
	private float _attackVisualReturnTimer;
	private bool _attackCommitted;
	private Player _targetPlayer;

	private ToolItem _equippedTool;

	private AnimationTree _animTree;
	private EntityAnimationController _animationController;
	private CombatController _combatController;

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

	public override void TickAI(double delta)
	{
		var dt = (float)delta;

		_recoveryTimer -= dt;

		if (_combatController.Tick(dt))
			_animationController.ReturnToIdle();

		UpdateTarget();

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

				if (chaseDist < AttackStartRange && _recoveryTimer <= 0f && _combatController.CanSwing)
				{
					State = MobState.Attack;
					_windupTimer = AttackWindup;
					_attackCommitted = false;
					MoveVelocity = Vector3.Zero;
				}

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
						FinishAttack();
						State = MobState.Chase;
					}
				}

				break;
		}
	}

	protected override void TickVisuals(float dt)
	{
		var horizontalVelocity = Velocity;
		horizontalVelocity.Y = 0f;

		_animationController.SetLocomotionState(horizontalVelocity.LengthSquared() > 0.01f);
		_animationController.Tick(1f - Mathf.Exp(-14f * dt));

		if (_attackVisualReturnTimer <= 0f)
			return;

		_attackVisualReturnTimer -= dt;
		if (_attackVisualReturnTimer <= 0f)
			_animationController.ReturnToIdle();
	}

	public override void PlayRemoteAttackVisual()
	{
		_animationController.PlayUseTool(_equippedTool, GlobalTransform.Basis.Z, false);
		_attackVisualReturnTimer = AttackRecovery;
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

	private bool TryLightAttack()
	{
		if (!_combatController.CanSwing || !HasValidTarget())
			return false;

		var swingDir = _targetPlayer.GlobalPosition - GlobalPosition;
		if (swingDir.LengthSquared() < 0.001f)
			return false;

		var angleOffset = (float)Mathf.DegToRad(GD.RandRange(-5f, 5f));
		swingDir = swingDir.Rotated(Vector3.Up, angleOffset).Normalized();

		_combatController.StartAttack();
		_combatController.StartCooldown(_equippedTool, _cooldownMultiplier);
		_animationController.PlayUseTool(_equippedTool, swingDir, false);
		_attackVisualReturnTimer = AttackRecovery;

		World.Sync.BroadcastMobAttack(Uid);
		World.ResolveEnemyMeleeAttack(this, _equippedTool, swingDir, _toolQuery);
		return true;
	}

	private void FinishAttack()
	{
		_combatController.EndAttack();
		_animationController.ReturnToIdle();
		_attackCommitted = false;
		_attackVisualReturnTimer = 0f;
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

	public void OnAttackHoldFrame()
	{
	}
}