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
        new(ApplicationCommandId.SelectionAll, new("A", KeyModifiers.Control)),
        new(ApplicationCommandId.SelectionClearAll, new("Escape")),
        new(ApplicationCommandId.SelectionClear, new("Escape")),
        new(ApplicationCommandId.FileCopy, new("C")),
        new(ApplicationCommandId.FileMove, new("M")),
        new(ApplicationCommandId.FileDelete, new("D")),
        new(ApplicationCommandId.FileRename, new("R")),
        new(ApplicationCommandId.FileAttributes, new("A")),
        new(ApplicationCommandId.LogToggle, new("V")),
        new(ApplicationCommandId.DialogConfirm, new("Enter")),
        new(ApplicationCommandId.DialogCancel, new("Escape")),
        new(ApplicationCommandId.DriveChange, new("L")),
        new(ApplicationCommandId.DirectoryCreate, new("K")),
        new(ApplicationCommandId.FileCreate, new("N")),
        new(ApplicationCommandId.DirectoryRoot, new("OemPipe")),
        new(ApplicationCommandId.PaneSyncOpposite, new("T")),
        new(ApplicationCommandId.FileFilter, new("P")),
        new(ApplicationCommandId.EditorLaunch, new("E")),
        new(ApplicationCommandId.FileDuplicate, new("W")),
        new(ApplicationCommandId.ViewSort, new("S")),
        new(ApplicationCommandId.AppExit, new("Q")),
        new(ApplicationCommandId.FileExecute, new("X")),
        new(ApplicationCommandId.FileCopyPath, new("C", KeyModifiers.Control)),
        new(ApplicationCommandId.TerminalOpen, new("Oem2")),
        new(ApplicationCommandId.FileSearch, new("F")),
        new(ApplicationCommandId.ViewRefresh, new("F5")),
        new(ApplicationCommandId.DirectoryBookmark, new("D0")),
        new(ApplicationCommandId.DirectoryHistory, new("H")),
        new(ApplicationCommandId.FileProperties, new("O")),
        new(ApplicationCommandId.FileCompare, new("J")),
        new(ApplicationCommandId.AppSettings, new("OemComma", KeyModifiers.Control)),
    ];
}
