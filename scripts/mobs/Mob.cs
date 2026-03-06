using Godot;

public partial class Mob : CharacterBody3D, IToolHittable
{
	[Export] public float MaxHealth = 10f;

	public ulong Uid { get; private set; }
	private float _health;

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
		QueueFree();
	}

	public void SetUid(ulong uid)
	{
		Uid = uid;
	}
}