using Godot;
using System.Collections.Generic;

public partial class MenuManager : Node
{
	public static MenuManager Instance { get; private set; }

	private readonly Stack<Control> _stack = new();

	private ColorRect _blur;
	private ShaderMaterial _blurMat;
	private float _currentBlur;
	private float _maxBlur = 2f;
	private float _tweenTime = 0.25f;

	public bool HasMenus => _stack.Count > 0;


	public override void _Ready()
	{
		Instance = this;

		_blur = GetNode<ColorRect>("BlurOverlay");
		_blurMat = (ShaderMaterial)_blur.Material;
		_blur.Visible = false;
	}

	private void ShowBlur()
	{
		_blur.Visible = true;
		TweenBlurTo(_maxBlur);
	}

	private void HideBlur()
	{
		var tween = TweenBlurTo(0f);
		tween.Finished += () => { _blur.Visible = false; };
	}

	private void UpdateBlur(float value)
	{
		_currentBlur = value;
		_blurMat.SetShaderParameter("blur_amount", _currentBlur);
	}

	private Tween TweenBlurTo(float target)
	{
		var tween = CreateTween()
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		tween.TweenMethod(
			new Callable(this, nameof(UpdateBlur)),
			_currentBlur,
			target,
			_tweenTime
		);

		_currentBlur = target;
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