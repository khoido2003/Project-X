using System;
using System.Collections.Generic;

[Flags]
public enum ActionFlag
{
    None,
    SkillPreview,
    UIBlocked,
    Aiming,
    Dashing,
    Interacting,
}

public class ActionFlagComponent
{
    private readonly Dictionary<ActionFlag, bool> _flags = new();

    public void Set(ActionFlag flag, bool value)
    {
        _flags[flag] = value;
    }

    public bool Get(ActionFlag flag)
    {
        return _flags.TryGetValue(flag, out bool v) && v;
    }

    public void Clear(ActionFlag flag)
    {
        _flags.Remove(flag);
    }

    public void ClearAll()
    {
        _flags.Clear();
    }

    public override string ToString()
    {
        return $"[ActionFlags] {string.Join(", ", _flags)}";
    }
}
