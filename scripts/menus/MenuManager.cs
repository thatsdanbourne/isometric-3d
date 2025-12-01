using Godot;
using System.Collections.Generic;

public partial class MenuManager : Node
{
    public static MenuManager Instance { get; private set; }

    private readonly Stack<Control> _stack = new();

    public bool HasMenus => _stack.Count > 0;

    public override void _Ready()
    {
        Instance = this;
    }

    public void Push(Control menu)
    {
        if (_stack.Count > 0)
            _stack.Peek().Visible = false;

        _stack.Push(menu);
        menu.Visible = true;
    }

    public void Pop()
    {
        if (_stack.Count == 0)
            return;

        var closing = _stack.Pop();
        closing.Visible = false;

        if (_stack.Count > 0)
            _stack.Peek().Visible = true;
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
    }
}