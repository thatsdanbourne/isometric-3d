using Godot;
using System.Collections.Generic;

public partial class WorldDecor : Node3D
{
    private static readonly Dictionary<Texture2D, StandardMaterial3D> MaterialCache = new Dictionary<Texture2D, StandardMaterial3D>();
    private static readonly StandardMaterial3D BaseMaterial = GD.Load<StandardMaterial3D>("res://resources/materials/WorldObjectBase.tres");

    private Node3D visual;
    private RandomNumberGenerator rng = new RandomNumberGenerator();


    public override void _Ready()
    {
        visual = GetNode<Node3D>("Sprite3D") ?? GetNode<Node3D>("AnimatedSprite3D");

        rng.Randomize();
        Translate(new Vector3(0f, 0f, rng.RandfRange(-0.01f, 0.01f)));

        ApplyDeterministicFlip();

        if (visual is Sprite3D vs)
        {
            vs.PixelSize *= DeterministicRandom(0.8f, 1f);
        }

        ApplySpriteMaterial();
    }

    private void ApplySpriteMaterial()
    {
        // Find a Sprite3D or AnimatedSprite3D
        if (visual == null)
            return;

        Texture2D tex = null;

        if (visual is Sprite3D sprite3D)
            tex = sprite3D.Texture;
        else if (visual is AnimatedSprite3D animSprite)
            tex = animSprite.SpriteFrames.GetFrameTexture("idle", 0);

        if (tex == null)
            return;

        if (!MaterialCache.TryGetValue(tex, out StandardMaterial3D mat))
        {
            mat = (StandardMaterial3D)BaseMaterial.Duplicate();
            mat.AlbedoTexture = tex;
            MaterialCache[tex] = mat;
        }

        if (visual is Sprite3D s3d)
        {
            s3d.MaterialOverride = mat;
            s3d.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        else if (visual is AnimatedSprite3D as3d)
        {
            as3d.MaterialOverride = mat;
            as3d.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    public void ApplyDeterministicFlip()
    {
        int x = Mathf.FloorToInt(GlobalPosition.X);
        int z = Mathf.FloorToInt(GlobalPosition.Z);

        uint hash = (uint)(x * 73856093) ^ (uint)(z * 19349663);
        bool flip = (hash & 1) == 1;

        if (visual is Sprite3D sprite)
            sprite.FlipH = flip;
    }


    float DeterministicRandom(float min, float max)
    {
        uint hash = (uint)(Mathf.FloorToInt(GlobalPosition.X) * 73856093) ^ (uint)(Mathf.FloorToInt(GlobalPosition.Z) * 19349663);
        float t = (hash % 1000) / 1000f; // 0–1 range
        return Mathf.Lerp(min, max, t);
    }
}
