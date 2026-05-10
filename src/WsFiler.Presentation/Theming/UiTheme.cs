using System;

namespace WsFiler.Presentation.Theming;

public static class UiTheme
{
    private static bool isLight;

    public static event Action? Changed;

    public static bool IsLight
    {
        get => isLight;
        set
        {
            if (isLight == value)
            {
                return;
            }

            isLight = value;
            Changed?.Invoke();
        }
    }
}
