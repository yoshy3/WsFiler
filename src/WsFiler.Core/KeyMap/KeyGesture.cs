namespace WsFiler.Core.KeyMap;

public sealed record KeyGesture(string Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    public override string ToString()
    {
        if (Modifiers == KeyModifiers.None)
        {
            return Key;
        }

        var parts = new List<string>();

        if (Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Meta");
        }

        parts.Add(Key);
        return string.Join("+", parts);
    }
}
