using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;

public partial class ItemPickup : Node3D
{
	private static readonly Dictionary<Texture2D, StandardMaterial3D> MaterialCache = new Dictionary<Texture2D, StandardMaterial3D>();
	public Item Item;
	public int Count = 1;

	private Area3D area;
	private Sprite3D sprite;
	private Sprite3D shadow;

	private const float MagnetSpeedBase = 1f;
	private const float MagnetSpeedMax = 20f;

	private float pickupDelay = 0.5f;
	private float delayTimer = 0f;

	private float collectRadius = 0.25f;

	private Vector3 velocity = Vector3.Zero;
	private float verticalHeight = 0f;
	private float verticalVelocity = 0f;

	private const float LaunchStrength = 6f;
	private const float BounceHeight = 1.2f;

	private float hoverPhase = 0f;
	private const float HoverAmplitude = 0.15f;
	private const float HoverSpeed = 3f;

	private CharacterBody3D target;
	private bool bouncing = true;
	private bool magnetEnabled = false;
	private bool collected = false;

	private static readonly Material BaseMat = GD.Load<Material>("res://resources/materials/WorldObjectBase.tres");
	private static Texture2D ShadowTex;

	public override void _Ready()
    {
        area = GetNode<Area3D>("Area3D");
		sprite = GetNode<Sprite3D>("Sprite3D");
		shadow = GetNode<Sprite3D>("Shadow");

		GlobalPosition += new Vector3(0f, 0.2f, 0f);

		delayTimer = pickupDelay;

		sprite.Texture = Item.Icon;

		if (!MaterialCache.TryGetValue(Item.Icon, out var mat))
		{
			mat = (StandardMaterial3D)BaseMat.Duplicate();

			mat.AlbedoTexture = Item.Icon;
			mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
			MaterialCache[Item.Icon] = mat;
		}

		sprite.MaterialOverride = mat;
		sprite.PixelSize = 0.025f;
		sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

		if (ShadowTex == null)
			ShadowTex = MakeRadialShadow(128);
		shadow.Texture = ShadowTex;
		shadow.Position = new Vector3(0f, 0.01f, 0f);
		shadow.RotationDegrees = new Vector3(-90f, 0f, 0f);

		var rng = new RandomNumberGenerator();
		rng.Randomize();

		var dir = new Vector3(
			rng.RandfRange(-1f, 1f),
			0f,
			rng.RandfRange(-1f, 1f)
		).Normalized();

		velocity = dir * LaunchStrength;
		verticalVelocity = BounceHeight * 8f;

		area.BodyEntered += OnBodyEntered;
    }

	private void OnBodyEntered(Node3D body)
    {
        if (collected) return;
		
		if (delayTimer <= 0 && body is CharacterBody3D character)
			target = character;
    }

	public override void _PhysicsProcess(double deltaRaw)
    {
        if (collected) return;

		float delta = (float)deltaRaw;

		if (bouncing)
			BounceMotion(delta);
		else
			HoverMotion(delta);
		
		if (!magnetEnabled)
        {
            UpdateSpriteOffset();
			UpdateShadowScale();
        }

		if (delayTimer > 0)
        {
            delayTimer -= delta;

			if (delayTimer <= 0)
            {
                foreach (var body in area.GetOverlappingBodies())
                {
                    if (body is CharacterBody3D character)
                       target = character;
                    
                }
            }
        }

		if (target != null)
        {
            MagnetToTarget(delta);
			return;
        }
    }

	private void BounceMotion(float delta)
    {
        GlobalPosition += velocity * delta;
		velocity = velocity.Lerp(Vector3.Zero, delta * 3f);

		verticalVelocity -= 20f * delta;
		verticalHeight += verticalVelocity * delta;

		if (verticalHeight <= 0f)
        {
            verticalHeight = 0f;
			bouncing = false;
        }
    }

	private void HoverMotion(float delta)
    {
        hoverPhase += delta * HoverSpeed;
		verticalHeight = Mathf.Sin(hoverPhase) * HoverAmplitude;
    }

	private void MagnetToTarget(float delta)
	{
		magnetEnabled = true;
		bouncing = false;

		sprite.Position = sprite.Position.Lerp(
			new Vector3(sprite.Position.X, 0.4f, sprite.Position.Z),
			0.05f
		);

		var targetPos = target.GlobalPosition;

		float dist = sprite.GlobalPosition.DistanceTo(targetPos);
		float distFactor = Mathf.Clamp(1f - dist / 10f, 0f, 1f);
		float speed = Mathf.Lerp(MagnetSpeedBase, MagnetSpeedMax, distFactor);

		GlobalPosition = GlobalPosition.Lerp(targetPos, speed * delta);
		
		if (GlobalPosition.DistanceTo(targetPos) < collectRadius)
			Collect();
	}

	private async void Collect()
    {
        if (collected) return;
		collected = true;

		area.Monitorable = false;
		area.Monitoring = false;

		if (target is Player p)
        {
            p.CollectItem(Item, Count);
			AudioManager.Instance.PlayAt("pickup_pop", GlobalPosition, 0.1f);
        }

		// shrink tween
		var t = CreateTween().SetParallel();
		t.TweenProperty(sprite, "pixel_size", 0.001f, 0.1f);
		t.TweenProperty(shadow, "scale", Vector3.Zero, 0.1f);

		await ToSignal(t, Tween.SignalName.Finished);

		QueueFree();
    }

	private void UpdateSpriteOffset()
	{
		var p = sprite.Position;
		sprite.Position = new Vector3(p.X, verticalHeight + 0.4f, p.Z);
	}

	private void UpdateShadowScale()
	{
		float shadowScale = Mathf.Clamp(1f - verticalHeight * 0.7f, 0.4f, 1f);
		shadow.Scale = new Vector3(shadowScale, shadowScale, shadowScale);
	}

	private Texture2D MakeRadialShadow(int size)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        float half = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp(1f - d, 0f, 1f);

                img.SetPixel(x, y, new Color(0, 0, 0, alpha * 0.5f));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
