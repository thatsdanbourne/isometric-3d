using Godot;
using System.Collections.Generic;

public partial class MenuManager : Node
{
    public static MenuManager Instance { get; private set; }

    private readonly Stack<Control> _stack = new();

    private ColorRect Blur;
    private ShaderMaterial blurMat;
    private float currentBlur = 0f;
    private float maxBlur = 2f;
    private float tweenTime = 0.25f;

    public bool HasMenus => _stack.Count > 0;


    public override void _Ready()
    {
        Instance = this;

        Blur = GetNode<ColorRect>("BlurOverlay");
        blurMat = (ShaderMaterial)Blur.Material;
        Blur.Visible = false;
    }

    private void ShowBlur()
    {
        Blur.Visible = true;
        TweenBlurTo(maxBlur);
    }

    private void HideBlur()
    {
        Tween tween = TweenBlurTo(0f);
        tween.Finished += () => { Blur.Visible = false; };
    }
    
    private void UpdateBlur(float value)
    {
        currentBlur = value;
        blurMat.SetShaderParameter("blur_amount", currentBlur);
    }

    private Tween TweenBlurTo(float target)
    {
        Tween tween = CreateTween()
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        
        tween.TweenMethod(
            new Callable(this, nameof(UpdateBlur)), 
            currentBlur, 
            target, 
            tweenTime
        );

        currentBlur = target;
        return tween;
    }

    public void Push(Control menu)
    {
        if (_stack.Count > 0)
            _stack.Peek().Visible = false;

        _stack.Push(menu);
        menu.Visible = true;

        if (_stack.Count == 1)
            ShowBlur();
    }

    public void Pop()
    {
        if (_stack.Count == 0)
            return;

        var closing = _stack.Pop();
        closing.Visible = false;

        if (_stack.Count > 0)
            _stack.Peek().Visible = true;
        else 
            HideBlur();
    }

    public Control Peek()
    {
        if (_stack.Count == 0)
            return null;

        return _stack.Peek();
    }

    public void ClearStack()
    {
        while (_stack.Count > 0)
        {
            var m = _stack.Pop();
            m.Visible = false;
        }

        HideBlur();
    }
}