using Godot;

public partial class BlurOverlay : Control
{
    private ShaderMaterial _mat;

    public override void _Ready()
    {
        _mat = GetNode<ColorRect>("ColorRect").Material as ShaderMaterial;
        Visible = false;
        _mat.SetShaderParameter("radius", 0.0f);
    }

    public void FadeIn(float duration = 0.15f)
    {
        Visible = true;

        var tween = CreateTween();
        tween.TweenMethod(
            new Callable(this, nameof(SetStrength)),
            0.0f,
            6.0f,
            duration
        );
    }

    public async void FadeOut(float duration = 0.15f)
    {
        var tween = CreateTween();
        tween.TweenMethod(
            new Callable(this, nameof(SetStrength)),
            6.0f,
            0.0f,
            duration
        );

        await ToSignal(tween, Tween.SignalName.Finished);
        Visible = false;
    }

    private void SetStrength(float v)
    {
        _mat.SetShaderParameter("radius", v);
    }
}