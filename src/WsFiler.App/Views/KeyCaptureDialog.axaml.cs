using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class KeyCaptureDialog : Window
{
    private string? capturedGesture;

    public KeyCaptureDialog()
    {
        InitializeComponent();

        Title = Strings.Dialog_Keymap_Capture_Title;
        PromptLabel.Text = Strings.Dialog_Keymap_Capture_Prompt;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
        CapturedLabel.Text = "";

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or
                     Key.LeftShift or Key.RightShift or
                     Key.LeftAlt or Key.RightAlt or
                     Key.LWin or Key.RWin)
        {
            return;
        }

        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Close(null);
            return;
        }

        e.Handled = true;
        capturedGesture = FormatGesture(e.Key, e.KeyModifiers);
        CapturedLabel.Text = capturedGesture;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(capturedGesture);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private static string FormatGesture(Key key, KeyModifiers modifiers)
    {
        var keyText = key switch
        {
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Space => "Space",
            Key.Escape => "Escape",
            _ => key.ToString(),
        };

        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(KeyModifiers.Meta)) parts.Add("Meta");
        parts.Add(keyText);
        return string.Join("+", parts);
    }
}
