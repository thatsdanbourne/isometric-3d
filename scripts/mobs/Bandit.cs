using Godot;
using System;

public partial class Bandit : Mob
{
	private enum State
	{
		Idle,
		Chase,
		Attack
	}

	private State _state = State.Idle;

	public float AttackStartRange;
	public float AggroRange = 15f;
	public float MoveSpeed = 3f;
	public float AttackWindup = 0.25f;
	public float AttackRecovery = 0.35f;

	private float _cooldownMultiplier = 1.5f;
	private float _attackTimer;
	private float _windupTimer;
	private float _recoveryTimer;
	private bool _attackCommitted;
	private Player _player;

	private ToolItem _equippedTool;

	private AnimationTree _animTree;
	private const string LocomotionBlendPath = "parameters/Locomotion/blend_position";
	private const string AxeRequestPath = "parameters/AxeOS/request";

	public override void _Ready()
	{
		base._Ready();
		_animTree = GetNode<AnimationTree>("AnimationTree");
		_player = World.Player;
		_equippedTool = ItemRegistry.GetItem("stone_axe") as ToolItem;
		AttackStartRange = _equippedTool!.HitRange - 0.3f;
	}

	public override void TickAI(double delta)
	{
		if (_player == null) return;

		var dt = (float)delta;

		_attackTimer -= dt;
		_recoveryTimer -= dt;

		var dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

		switch (_state)
		{
			case State.Idle:
				MoveVelocity = Vector3.Zero;
				if (dist < AggroRange) _state = State.Chase;
				break;

			case State.Chase:
				MoveTowardsPlayer();

				if (dist < AttackStartRange && _recoveryTimer <= 0)
				{
					_state = State.Attack;
					_windupTimer = AttackWindup;
					_attackCommitted = false;
					MoveVelocity = Vector3.Zero;
				}
				else if (dist > AggroRange)
				{
					_state = State.Idle;
				}

				break;

			case State.Attack:
				FacePlayer();

				if (dist > _equippedTool.HitRange + 0.5f)
				{
					_state = State.Chase;
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
						_state = State.Chase;
						_attackCommitted = false;
					}
				}

				break;
		}

		UpdateAnimation();
	}

	private void MoveTowardsPlayer()
	{
		var dir = _player.GlobalPosition - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.001f) return;
		var targetAngle = Mathf.Atan2(dir.X, dir.Z);

		Rotation = new Vector3(
			Rotation.X,
			Mathf.LerpAngle(Rotation.Y, targetAngle, 10f * (float)GetPhysicsProcessDeltaTime()),
			Rotation.Z
		);

		MoveVelocity = dir.Normalized() * MoveSpeed;
	}

	private void UpdateAnimation()
	{
		_animTree.Set(LocomotionBlendPath, MoveVelocity.Length());
	}

	private void FacePlayer()
	{
		var dir = _player.GlobalPosition - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.001f) return;
		var targetAngle = Mathf.Atan2(dir.X, dir.Z);

		Rotation = new Vector3(
			Rotation.X,
			Mathf.LerpAngle(Rotation.Y, targetAngle, 10f * (float)GetPhysicsProcessDeltaTime()),
			Rotation.Z
		);
	}

	private async void TryAttack()
	{
		if (_attackTimer > 0f) return;

		_attackTimer = _equippedTool.CooldownSeconds * _cooldownMultiplier;

		_animTree.Set(AxeRequestPath, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

		var dir = (_player.GlobalPosition - GlobalPosition).Normalized();
		_equippedTool.UseOn(_player, dir, _player.GlobalPosition);
	}

	public override void ApplyKnockback(Vector3 direction, float strength)
	{
		base.ApplyKnockback(direction, strength);

		if (_state == State.Attack && !_attackCommitted)
		{
			_state = State.Chase;
			_windupTimer = 0f;
			_attackCommitted = false;
		}
	}
}