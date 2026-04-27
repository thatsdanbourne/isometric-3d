using Godot;

public partial class Mob : CharacterBody3D, IToolHittable
{
	[Export] public float MaxHealth = 10f;

	public World World;
	public ulong Uid { get; private set; }
	public string MobId { get; private set; }
	public Vector2I RuntimeChunk { get; set; }
	public Vector2I? SavedChunk { get; internal set; }

	public float CurrentHealth;
	protected Vector3 MoveVelocity;
	protected float _knockbackResistance = 1f;
	private float _knockbackDecay = 14f;
	private Vector3 _knockbackVelocity;

	protected Vector3 _netTargetPosition;
	protected Vector3 _netVelocity;
	protected MobState _netState;


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
		_netTargetPosition = GlobalPosition;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!World.Multiplayer.IsServer())
		{
			UpdateRemoteMotion(delta);
			MoveAndSlide();
			return;
		}

		_knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, _knockbackDecay * (float)delta);
		Velocity = MoveVelocity + _knockbackVelocity;
		MoveAndSlide();
		World.MobStreamer.UpdateMobChunkMembership(this);
	}

	public virtual void TickAI(double delta)
	{
	}

	public Node3D GetHitRoot()
	{
		return this;
	}

	private void UpdateRemoteMotion(double delta)
	{
		GlobalPosition = GlobalPosition.Lerp(_netTargetPosition, 12f * (float)delta);

		if (_netVelocity.LengthSquared() > 0.001f)
		{
			var targetAngle = Mathf.Atan2(_netVelocity.X, _netVelocity.Z);
			Rotation = new Vector3(Rotation.X, targetAngle, Rotation.Z);
		}

		ApplyRemoteStateVisuals();
	}

	protected virtual void ApplyRemoteStateVisuals()
	{
	}

	public void ApplyRemoteSnapshot(Vector3 position, Vector3 velocity, int state, float health)
	{
		_netTargetPosition = position;
		_netVelocity = velocity;
		_netState = (MobState)state;
		CurrentHealth = health;
	}

	public virtual void PlayRemoteAttackVisual()
	{
	}

	public ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, Vector3 fromDirection, Vector3 hitPoint)
	{
		ApplyKnockback(fromDirection, 6f);
		CurrentHealth -= damage;
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
		direction.Y = 0;

		if (direction.LengthSquared() < 0.001f) return;

		_knockbackVelocity += direction.Normalized() * (strength / _knockbackResistance);
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

	public string GetHitSound()
	{
		return "hit_flesh";
	}

	public string GetBreakSound()
	{
		return "hit_flesh";
	}
}