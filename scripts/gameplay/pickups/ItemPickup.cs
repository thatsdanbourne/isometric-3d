using Godot;
using System.Collections.Generic;

public partial class ItemPickup : Node3D
{
	public ulong PickupId;

	public Vector3 InitialVelocity;
	public float InitialVerticalVelocity;

	public Item Item;
	public int Count = 1;

	private static readonly Dictionary<Texture2D, StandardMaterial3D> MaterialCache = new();

	private Area3D _area;
	private MeshInstance3D _meshInstance;
	private Sprite3D _shadow;

	private const float MagnetSpeedBase = 1f;
	private const float MagnetSpeedMax = 20f;

	private float _pickupDelay = 0.5f;
	private float _delayTimer;

	private float _collectRadius = 0.25f;

	private Vector3 _velocity = Vector3.Zero;
	private float _verticalHeight;
	private float _verticalVelocity;

	public const float LaunchStrength = 6f;
	public const float BounceHeight = 1.2f;

	private float _hoverPhase;
	private const float HoverAmplitude = 0.15f;
	private const float HoverSpeed = 3f;

	private const float DespawnTime = 300f;
	private float _lifeTimer;

	private CharacterBody3D _target;
	private bool _bouncing = true;
	private bool _magnetEnabled;
	private bool _collected;

	private static readonly Material BaseMat = GD.Load<Material>("res://resources/materials/WorldObjectBase.tres");
	private static Texture2D _shadowTex;

	public override void _Ready()
	{
		_area = GetNode<Area3D>("Area3D");
		_meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
		_shadow = GetNode<Sprite3D>("Shadow");

		GlobalPosition += new Vector3(0f, 0.2f, 0f);

		_delayTimer = _pickupDelay;

		if (!MaterialCache.TryGetValue(Item.Icon, out var mat))
		{
			mat = (StandardMaterial3D)BaseMat.Duplicate();

			mat.AlbedoTexture = Item.Icon;
			mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
			mat.BillboardKeepScale = true;
			MaterialCache[Item.Icon] = mat;
		}

		_meshInstance.MaterialOverride = mat;
		_meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		_shadowTex ??= MakeRadialShadow(128);
		_shadow.Texture = _shadowTex;
		_shadow.Position = new Vector3(0f, 0.01f, 0f);
		_shadow.RotationDegrees = new Vector3(-90f, 0f, 0f);

		_velocity = InitialVelocity;
		_verticalVelocity = InitialVerticalVelocity;

		_area.BodyEntered += OnBodyEntered;
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_collected) return;

		if (_delayTimer <= 0 && body is CharacterBody3D character)
			_target = character;
	}

	public override void _PhysicsProcess(double deltaRaw)
	{
		if (_collected) return;

		var delta = (float)deltaRaw;

		_lifeTimer += delta;
		if (_lifeTimer >= DespawnTime)
		{
			QueueFree();
			return;
		}

		if (_bouncing)
			BounceMotion(delta);
		else
			HoverMotion(delta);

		if (!_magnetEnabled)
		{
			UpdateSpriteOffset();
			UpdateShadowScale();
		}

		if (_delayTimer > 0)
		{
			_delayTimer -= delta;

			if (_delayTimer <= 0)
				foreach (var body in _area.GetOverlappingBodies())
					if (body is CharacterBody3D character)
						_target = character;
		}

		if (_target != null)
			MagnetToTarget(delta);
	}

	private void BounceMotion(float delta)
	{
		GlobalPosition += _velocity * delta;
		_velocity = _velocity.Lerp(Vector3.Zero, delta * 3f);

		_verticalVelocity -= 20f * delta;
		_verticalHeight += _verticalVelocity * delta;

		if (_verticalHeight <= 0f)
		{
			_verticalHeight = 0f;
			_bouncing = false;
		}
	}

	private void HoverMotion(float delta)
	{
		_hoverPhase += delta * HoverSpeed;
		_verticalHeight = Mathf.Sin(_hoverPhase) * HoverAmplitude;
	}

	private void MagnetToTarget(float delta)
	{
		_magnetEnabled = true;
		_bouncing = false;

		_meshInstance.Position = _meshInstance.Position.Lerp(
			new Vector3(_meshInstance.Position.X, 0.4f, _meshInstance.Position.Z),
			0.05f
		);

		var targetPos = _target.GlobalPosition;

		var dist = _meshInstance.GlobalPosition.DistanceTo(targetPos);
		var distFactor = Mathf.Clamp(1f - dist / 10f, 0f, 1f);
		var speed = Mathf.Lerp(MagnetSpeedBase, MagnetSpeedMax, distFactor);

		GlobalPosition = GlobalPosition.Lerp(targetPos, speed * delta);

		if (GlobalPosition.DistanceTo(targetPos) < _collectRadius)
			Collect();
	}

	private void Collect()
	{
		if (_collected) return;

		if (_target is not Player p)
			return;

		if (!p.IsLocal)
			return;

		p.RequestItemPickup(PickupId);
	}

	public void AnimateOut()
	{
		_collected = true;
		_area.Monitorable = false;
		_area.Monitoring = false;

		AudioManager.Instance.PlayAt("pickup_pop", GlobalPosition, 0.1f);

		// shrink tween
		var t = CreateTween();
		var scale = new Vector3(0.01f, 0.01f, 0.01f);
		t.TweenProperty(_meshInstance, "scale", scale, 0.15f);

		t.Finished += QueueFree;
	}

	private void UpdateSpriteOffset()
	{
		var p = _meshInstance.Position;
		_meshInstance.Position = new Vector3(p.X, _verticalHeight + 0.4f, p.Z);
	}

	private void UpdateShadowScale()
	{
		var shadowScale = Mathf.Clamp(1f - _verticalHeight * 0.7f, 0.4f, 1f);
		_shadow.Scale = new Vector3(shadowScale, shadowScale, shadowScale);
	}

	private Texture2D MakeRadialShadow(int size)
	{
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

		var half = size / 2f;

		for (var y = 0; y < size; y++)
		for (var x = 0; x < size; x++)
		{
			var dx = (x - half) / half;
			var dy = (y - half) / half;
			var d = Mathf.Sqrt(dx * dx + dy * dy);
			var alpha = Mathf.Clamp(1f - d, 0f, 1f);

			img.SetPixel(x, y, new Color(0, 0, 0, alpha * 0.5f));
		}

		return ImageTexture.CreateFromImage(img);
	}
}