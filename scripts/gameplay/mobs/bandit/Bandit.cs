using Godot;
using System;
using System.Runtime.InteropServices;

public partial class Bandit : Mob
{
	public float AttackStartRange;
	public float AggroRange = 15f;
	public float MoveSpeed = 4f;
	public float AttackWindup = 0.15f;
	public float AttackRecovery = 0.35f;

	private float _cooldownMultiplier = 1.5f;
	private float _attackTimer;
	private float _windupTimer;
	private float _recoveryTimer;
	private bool _attackCommitted;
	private Player _targetPlayer;

	private ToolItem _equippedTool;

	private AnimationTree _animTree;
	private const string LocomotionBlendPath = "parameters/Locomotion/blend_position";
	private const string AxeRequestPath = "parameters/AxeOS/request";

	private PhysicsRayQueryParameters3D _toolQuery;
	private const uint HittableMask = 1u << 1;

	private MobState _lastRemoteVisualState = MobState.Idle;

	public override void _Ready()
	{
		base._Ready();

		_animTree = GetNode<AnimationTree>("AnimationTree");
		_equippedTool = ItemRegistry.GetItem("stone_axe") as ToolItem;
		AttackStartRange = _equippedTool!.HitRange - 0.3f;

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

		_attackTimer -= dt;
		_recoveryTimer -= dt;

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

				MoveTowardsTarget();

				if (chaseDist < AttackStartRange && _recoveryTimer <= 0f)
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
					_attackCommitted = false;
					MoveVelocity = Vector3.Zero;
					break;
				}

				var attackDist = DistanceToTarget();

				FaceTarget();

				if (attackDist > _equippedTool.HitRange + 0.5f)
				{
					State = MobState.Chase;
					_attackCommitted = false;
					break;
				}

				MoveVelocity = Vector3.Zero;

				if (!_attackCommitted)
				{
					_windupTimer -= dt;

					if (_windupTimer <= 0)
					{
						_attackCommitted = true;
						TryAttack();
						_recoveryTimer = AttackRecovery;
					}
				}
				else
				{
					if (_attackTimer <= 0f)
					{
						State = MobState.Chase;
						_attackCommitted = false;
					}
				}

				break;
		}
	}

	protected override void ApplyRemoteStateVisuals()
	{
		ApplyAnimationForState(_netState, _netVelocity);
	}

	private void ApplyAnimationForState(MobState state, Vector3 velocity, bool forceAttack = false)
	{
		switch (state)
		{
			case MobState.Idle:
			case MobState.Chase:
				_animTree.Set(LocomotionBlendPath, velocity.Length());
				break;

			case MobState.Attack:
				_animTree.Set(LocomotionBlendPath, velocity.Length());
				
				if (forceAttack || _lastRemoteVisualState != MobState.Attack)
					_animTree.Set(AxeRequestPath, (int)AnimationNodeOneShot.OneShotRequest.Fire);

				break;
		}

		_lastRemoteVisualState = state;
	}

	public override void PlayRemoteAttackVisual()
	{
		AudioManager.Instance.PlayVariantAt("swing_blade_small", GlobalPosition, AudioManager.BusTools, 0.2f);
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

	private void MoveTowardsTarget()
	{
		if (!HasValidTarget())
		{
			MoveVelocity = Vector3.Zero;
			return;
		}

		var dir = _targetPlayer.GlobalPosition - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.001f) return;

		if (TryMoveDirection(dir, MoveSpeed, out MoveVelocity, out var chosenDir))
		{
			var targetAngle = Mathf.Atan2(chosenDir.X, chosenDir.Z);

			Rotation = new Vector3(
				Rotation.X,
				Mathf.LerpAngle(Rotation.Y, targetAngle, 10f * (float)GetPhysicsProcessDeltaTime()),
				Rotation.Z
			);
		}
		else
		{
			MoveVelocity = Vector3.Zero;
		}
	}

	private void FaceTarget()
	{
		if (!HasValidTarget()) return;

		var dir = _targetPlayer.GlobalPosition - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.001f) return;
		var targetAngle = Mathf.Atan2(dir.X, dir.Z);

		Rotation = new Vector3(
			Rotation.X,
			Mathf.LerpAngle(Rotation.Y, targetAngle, 10f * (float)GetPhysicsProcessDeltaTime()),
			Rotation.Z
		);
	}

	private void TryAttack()
	{
		if (_attackTimer > 0f) return;

		_attackTimer = _equippedTool.CooldownSeconds * _cooldownMultiplier;

		World.Sync.BroadcastMobAttack(Uid);

		var swingDir = _targetPlayer.GlobalPosition - GlobalPosition;
		if (swingDir.LengthSquared() < 0.001f)
			return;

		var angleOffset = (float)Mathf.DegToRad(GD.RandRange(-5f, 5f));
		swingDir = swingDir.Rotated(Vector3.Up, angleOffset).Normalized();

		World.ResolveEnemyMeleeAttack(this, _equippedTool, swingDir, _toolQuery);
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
}