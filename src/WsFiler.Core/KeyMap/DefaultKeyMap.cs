using WsFiler.Core.Commands;

namespace WsFiler.Core.KeyMap;

public static class DefaultKeyMap
{
    public static IReadOnlyList<KeyBinding> Bindings { get; } =
    [
        new(ApplicationCommandId.CursorUp, new("Up")),
        new(ApplicationCommandId.CursorDown, new("Down")),
        new(ApplicationCommandId.CursorLeft, new("Left")),
        new(ApplicationCommandId.CursorRight, new("Right")),
        new(ApplicationCommandId.CursorPageUp, new("PageUp")),
        new(ApplicationCommandId.CursorPageDown, new("PageDown")),
        new(ApplicationCommandId.CursorFirst, new("Home")),
        new(ApplicationCommandId.CursorLast, new("End")),
        new(ApplicationCommandId.DirectoryOpen, new("Enter")),
        new(ApplicationCommandId.FilePreview, new("Enter")),
        new(ApplicationCommandId.DirectoryParent, new("Backspace")),
        new(ApplicationCommandId.PaneSwitch, new("Tab")),
        new(ApplicationCommandId.SelectionToggle, new("Space")),
        new(ApplicationCommandId.SelectionAll, new("A")),
        new(ApplicationCommandId.SelectionClearAll, new("Escape")),
        new(ApplicationCommandId.SelectionClear, new("Escape")),
        new(ApplicationCommandId.FileCopy, new("C")),
        new(ApplicationCommandId.FileMove, new("M")),
        new(ApplicationCommandId.FileDelete, new("D")),
        new(ApplicationCommandId.FileRename, new("R")),
        new(ApplicationCommandId.LogToggle, new("V")),
        new(ApplicationCommandId.DialogConfirm, new("Enter")),
        new(ApplicationCommandId.DialogCancel, new("Escape")),
    ];
}
