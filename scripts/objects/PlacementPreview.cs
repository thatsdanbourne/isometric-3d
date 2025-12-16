using Godot;
using System;

public partial class PlacementPreview : Node3D
{
	private Sprite3D sprite;
	private AnimatedSprite3D animatedSprite;

	public override void _Ready()
	{
		sprite = GetNode<Sprite3D>("Sprite3D");
		animatedSprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");

		sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		sprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
		sprite.Shaded = true;

		animatedSprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		animatedSprite.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
		animatedSprite.Shaded = true;

		sprite.PixelSize = 0.05f;
		animatedSprite.PixelSize = 0.05f;
	}

	public void SetTexture(Texture2D texture)
	{
		if (sprite == null || animatedSprite == null)
		{
			// player likely spawned in with placable item already equipped
			sprite = GetNode<Sprite3D>("Sprite3D");
			animatedSprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
		}

		sprite.Position += new Vector3(0, texture.GetHeight() * 0.05f / 2f, 0);
		sprite.Texture = texture;
		sprite.Visible = true;
		animatedSprite.Visible = false;
	}

	public void SetAnimatedSprite(SpriteFrames spriteFrames)
	{
		if (animatedSprite == null || sprite == null)
		{
			// player likely spawned in with placable item already equipped
			animatedSprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
			sprite = GetNode<Sprite3D>("Sprite3D");
		}

		animatedSprite.Position += new Vector3(0, spriteFrames.GetFrameTexture("default", 0).GetHeight() * 0.05f / 2f, 0);
		animatedSprite.SpriteFrames = spriteFrames;
		animatedSprite.Play("default");

		animatedSprite.Visible = true;
		sprite.Visible = false;
	}

	public void SetValid(bool valid)
	{
		Color c = valid
			? new Color(0.3f, 1f, 0.3f, 0.6f)
			: new Color(1f, 0.3f, 0.3f, 0.6f);

		sprite.Modulate = c;
		animatedSprite.Modulate = c;
	}
}
