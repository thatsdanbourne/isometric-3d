using Godot;

public partial class Mob : CharacterBody3D, IToolHittable
{
	public float MaxHealth = 40f;

	private const float RemotePositionCorrection = 12f;
	private const float RemoteRotationCorrection = 12f;

	public World World;
	public ulong Uid { get; private set; }
	public string MobId { get; private set; }
	public Vector2I RuntimeChunk { get; set; }
	public Vector2I? SavedChunk { get; internal set; }

	public float CurrentHealth;
	public float MaxPoise = 8f;
	public float CurrentPoise;
	public float PoiseRecoveryPerSec = 2f;
	public float StaggerDuration = 1f;
	protected float KnockbackResistance = 1f;
	protected float KnockbackDecay = 14f;

	protected EntityMotor EntityMotor { get; private set; }

	protected Vector3 MoveVelocity;
	protected Vector3 NetTargetPosition;
	protected Vector3 NetTargetRotation;
	protected Vector3 NetVelocity;
	protected MobState NetState;

	protected float StaggerTimer;
	public bool IsStaggered => StaggerTimer > 0f;


	public MobState State { get; protected set; } = MobState.Idle;

	public void Initialise(ulong uid, string mobId, Vector2I? savedChunk = null)
	{
		Uid = uid;
		MobId = mobId;
		SavedChunk = savedChunk;
	}

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		CurrentPoise = MaxPoise;
		NetTargetPosition = GlobalPosition;
		NetTargetRotation = Rotation;

		EntityMotor = new EntityMotor
		{
			FootstepsEnabled = true,
			KnockbackResistance = KnockbackResistance,
			KnockbackDecay = KnockbackDecay
		};
		AddChild(EntityMotor);
		EntityMotor.Init(this, World);
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;

		if (!World.Multiplayer.IsServer())
		{
			UpdateRemoteMotion(dt);
			MoveAndSlide();
			TickVisuals(dt);
			return;
		}

		EntityMotor.Update(dt, MoveVelocity);
		MoveAndSlide();
		TickVisuals(dt);
		World.MobStreamer.UpdateMobChunkMembership(this);
	}

	public virtual void TickAI(double delta)
	{
	}

	protected virtual void TickVisuals(float dt)
	{
	}

	public Node3D GetHitRoot()
	{
		return this;
	}

	private void UpdateRemoteMotion(float dt)
	{
		var positionError = NetTargetPosition - GlobalPosition;
		var moveVelocity = NetVelocity + positionError * RemotePositionCorrection;

		Rotation = Rotation.Lerp(NetTargetRotation, RemoteRotationCorrection * dt);
		EntityMotor.Update(dt, moveVelocity);

		ApplyRemoteStateVisuals(dt);
	}

	protected virtual void ApplyRemoteStateVisuals(float dt)
	{
	}

	public void ApplyRemoteSnapshot(Vector3 position, Vector3 rotation, Vector3 velocity, int state, float health)
	{
		NetTargetPosition = position;
		NetTargetRotation = rotation;
		NetVelocity = velocity;
		NetState = (MobState)state;
		CurrentHealth = health;
	}

	public virtual void PlayRemoteAttackVisual()
	{
	}

	public ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, float knockback, float stagger,
		Vector3 fromDirection,
		Vector3 hitPoint)
	{
		CurrentHealth -= damage;
		ApplyKnockback(fromDirection, knockback);

		if (ApplyPoiseDamage(stagger))
		{
			OnStaggered(fromDirection);

			World.Sync.BroadcastMobStaggered(this, fromDirection);
		}

		if (!(CurrentHealth <= 0)) return ToolHitOutcome.Hit;

		Die();
		return ToolHitOutcome.Destroyed;
	}

	public ToolHitOutcome ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ToolHitOutcome.Failed;
	}

	public float ModifyIncomingToolDamage(ToolItem tool, float damage, float baseDamage)
	{
		return baseDamage;
	}

	public virtual void ApplyKnockback(Vector3 direction, float strength)
	{
		EntityMotor.ApplyKnockback(direction, strength);
	}

	protected bool ApplyPoiseDamage(float stagger)
	{
		if (stagger <= 0f)
			return false;

		CurrentPoise -= stagger;

		if (CurrentPoise > 0f)
			return false;

		CurrentPoise = MaxPoise;
		return true;
	}

	protected virtual void OnStaggered(Vector3 fromDirection)
	{
		MoveVelocity = Vector3.Zero;
		StaggerTimer = StaggerDuration;
	}

	public virtual void ApplyRemoteStagger(Vector3 fromDirection)
	{
		StaggerTimer = StaggerDuration;
	}

	private void Die()
	{
		World.MobStreamer.HandleMobDeath(this);
	}

	public void LoadFromSaveData(MobRecord data, Vector2I savedChunk)
	{
		Initialise(data.Uid, data.MobId, savedChunk);
		SavedChunk = savedChunk;
	}

	protected bool TryMoveDirection(Vector3 desiredDir, float speed, out Vector3 moveVelocity, out Vector3 chosenDir)
	{
		moveVelocity = Vector3.Zero;
		chosenDir = Vector3.Zero;

		desiredDir.Y = 0f;
		if (desiredDir.LengthSquared() < 0.001f)
			return false;

		desiredDir = desiredDir.Normalized();

		// Try straight first, then fan left/right
		float[] angles = { 0f, -45f, 45f, -60f, 60f, 180f };

		foreach (var angle in angles)
		{
			var dir = desiredDir.Rotated(Vector3.Up, Mathf.DegToRad(angle)).Normalized();
			if (!IsDirectionBlocked(dir))
			{
				chosenDir = dir;
				moveVelocity = dir * speed;
				return true;
			}
		}

		return false;
	}

	protected void TurnTowardsDirection(Vector3 direction, float turnSpeed, float dt)
	{
		direction.Y = 0f;

		if (direction.LengthSquared() < 0.001f)
			return;

		var targetAngle = Mathf.Atan2(direction.X, direction.Z);
		Rotation = new Vector3(
			Rotation.X,
			Mathf.LerpAngle(Rotation.Y, targetAngle, turnSpeed * dt),
			Rotation.Z
		);
	}

	protected bool IsDirectionBlocked(Vector3 dir)
	{
		var probeDistance = 1.5f;
		var probePos = GlobalPosition + dir.Normalized() * probeDistance;
		var tile = TileUtils.WorldToTile(probePos);

		return World.IsTileBlocked(tile);
	}

	public string GetImpactType()
	{
		return "flesh";
	}

	public string GetHitSound(ToolItem tool)
	{
		return tool.ToolType switch
		{
			"sword" => "hit_flesh_blade",
			"axe" => "hit_flesh_blade",
			_ => "hit_flesh"
		};
	}

	public string GetBreakSound()
	{
		return "hit_flesh";
	}
}