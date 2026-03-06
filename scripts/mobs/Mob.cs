using Godot;

public partial class Mob : CharacterBody3D, IToolHittable
{
	[Export] public float MaxHealth = 10f;

	public World World;
	public ulong Uid { get; private set; }
	public string MobId { get; private set; }
	public Vector2I SpawnChunk { get; private set; }
	public Vector2I? SavedChunk { get; internal set; }
	private float _health;


	public void Initialise(ulong uid, string mobId, Vector2I spawnChunk)
	{
		Uid = uid;
		MobId = mobId;
		SpawnChunk = spawnChunk;
		SavedChunk = null;
	}

	public override void _Ready()
	{
		_health = MaxHealth;
	}

	public Node3D GetHitRoot()
	{
		return this;
	}

	public ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, Vector3 fromDirection, Vector3 hitPoint)
	{
		_health -= damage;
		if (_health <= 0)
		{
			Die();
			return ToolHitOutcome.Destroyed;
		}

		return ToolHitOutcome.Hit;
	}

	public ToolHitOutcome ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ToolHitOutcome.Failed;
	}

	public float ModifyIncomingToolDamage(ToolItem tool, float damage, float baseDamage)
	{
		return baseDamage;
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
}