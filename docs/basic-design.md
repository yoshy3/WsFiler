# WsFiler Basic Design

## 1. Purpose

This document defines the basic design for the WsFiler MVP.
It translates `docs/requirements.md` into an initial architecture, module
structure, data model, command model, and UI behavior plan.

Initial technology targets:

- .NET 10.
- Avalonia 12.
- CommunityToolkit.Mvvm for view models.
- Microsoft.Extensions.Logging for logging.
- xUnit for tests.

If .NET 10 or Avalonia 12 causes implementation or package compatibility issues,
an older stable version may be considered.

## 2. Design Principles

- Keep file operation logic independent from Avalonia.
- Treat keyboard commands as first-class application actions.
- Keep pane state explicit and testable.
- Isolate platform-specific behavior behind interfaces.
- Prefer simple local file system support for MVP while leaving room for future
  providers.
- Make failure states visible instead of hiding file system errors.

## 3. Proposed Solution Structure

The MVP solution should use multiple projects to keep responsibilities clear.

```text
src/
  WsFiler.slnx
  WsFiler.App/
    Avalonia application entry point, views, styles, platform startup
  WsFiler.Presentation/
    ViewModels, UI-facing commands, dialog coordination
  WsFiler.Core/
    Domain models, value objects, interfaces, commands, KeyMap, use cases
  WsFiler.Infra/
    Local file system, settings persistence, logging, OS services
tests/
  WsFiler.Core.Tests/
  WsFiler.Infra.Tests/
```

For the first implementation pass, the projects may be created together even if
some are still thin.

## 4. Layer Responsibilities

### 4.1 WsFiler.App

Responsibilities:

- Configure Avalonia.
- Load application styles and theme resources.
- Create the main window.
- Register platform services.
- Bridge Avalonia input events into the KeyMap dispatcher.

This layer should not perform file operations directly.

### 4.2 WsFiler.Presentation

Responsibilities:

- Own view models used by Avalonia views.
- Use CommunityToolkit.Mvvm for observable properties and commands.
- Expose bindable pane state.
- Expose status messages and operation progress.
- Coordinate dialogs through abstractions.
- Translate UI events into application commands.

Important types:

- `MainWindowViewModel`
- `FilePaneViewModel`
- `FileItemViewModel`
- `StatusBarViewModel`
- `IUserInteractionService`

### 4.3 WsFiler.Core

Responsibilities:

- Define domain models and interfaces.
- Execute application commands.
- Load and validate KeyMap settings.
- Manage active pane and inactive pane interactions.
- Orchestrate copy, move, delete, and rename operations.
- Coordinate refresh behavior after file operations.
- Avoid references to Avalonia.
- Avoid infrastructure details.

Important types:

- `FilePanelState`
- `FileSystemItem`
- `FileSystemPath`
- `FileSelection`
- `FileOperationRequest`
- `FileOperationResult`
- `IFileSystemProvider`
- `ISettingsStore`
- `IClock`
- `ApplicationCommandId`
- `ApplicationCommandDispatcher`
- `KeyMap`
- `KeyBinding`
- `KeyGesture`
- `PaneController`
- `FileOperationService`
- `NavigationService`

### 4.4 WsFiler.Infra

Responsibilities:

- Implement local and UNC-aware file system access.
- Persist settings.
- Provide logging implementation.
- Provide platform-specific services.
- Provide localization resource loading.

Important types:

- `LocalFileSystemProvider`
- `JsonSettingsStore`
- `PlatformPathService`
- `FileLauncher`
- `SystemLogSink`
- `LocalizationService`

## 5. Main Runtime Model

The application owns one `MainWindowViewModel`.

`MainWindowViewModel` owns:

- `LeftPane`
- `RightPane`
- `ActivePaneSide`
- `StatusMessage`
- `CurrentOperation`

Each `FilePaneViewModel` reflects one `FilePanelState`.

`FilePanelState` contains:

- Current directory path.
- Ordered item list.
- Cursor index.
- Selected item paths.
- Sort column.
- Sort direction.
- Loading state.
- Last error, if any.

Only one pane is active at a time. File operations use:

- Source: active pane.
- Destination: inactive pane, when required.

## 6. File System Model

### 6.1 FileSystemPath

`FileSystemPath` is a value object wrapping a path string.

It should preserve platform path forms:

- Windows drive paths: `C:\Users\name`
- Windows UNC paths: `\\server\share\folder`
- POSIX paths: `/home/name`

It should not normalize paths in a way that breaks UNC roots or case-sensitive
file systems.

### 6.2 FileSystemItem

`FileSystemItem` represents one directory entry.

Suggested fields:

- `Name`
- `FullPath`
- `ItemType`
- `Size`
- `ModifiedAt`
- `Attributes`
- `IsHidden`
- `IsReadOnly`
- `IsSymbolicLink`

`ItemType` values:

- `File`
- `Directory`
- `Other`

### 6.3 IFileSystemProvider

The MVP provider interface should support:

- List directory entries.
- Check whether a path exists.
- Get parent directory.
- Copy file or directory.
- Move file or directory.
- Delete file or directory.
- Rename file or directory.

Operations should be asynchronous where the result can be slow or blocking.

## 7. Command Model

WsFiler commands are identified by stable string IDs.

MVP command IDs:

- `pane.switch`
- `cursor.up`
- `cursor.down`
- `cursor.left`
- `cursor.right`
- `cursor.pageUp`
- `cursor.pageDown`
- `cursor.first`
- `cursor.last`
- `directory.open`
- `directory.parent`
- `file.preview`
- `selection.toggle`
- `selection.all`
- `selection.clearAll`
- `selection.clear`
- `file.copy`
- `file.move`
- `file.delete`
- `file.rename`
- `dialog.confirm`
- `dialog.cancel`

Each command handler should:

- Validate whether the command can run.
- Execute without depending on Avalonia controls.
- Return a result that can update status messages.
- Avoid throwing user-facing file system errors past the application boundary.

## 8. KeyMap Design

### 8.1 KeyMap Files

The MVP should support:

- Built-in default KeyMap.
- User override file.

Suggested user settings location:

- Windows: `%APPDATA%\WsFiler\settings.json`
- macOS: `~/Library/Application Support/WsFiler/settings.json`
- Linux: `~/.config/WsFiler/settings.json`

All user settings should be stored in a single `settings.json` file.

Suggested settings shape:

```json
{
  "language": "auto",
  "theme": "system",
  "restoreSession": true,
  "session": {
    "leftPath": "",
    "rightPath": ""
  },
  "keyMap": {
    "pane.switch": "Ctrl+I",
    "cursor.up": "Up",
    "cursor.down": "Down",
    "cursor.left": "Left",
    "cursor.right": "Right",
    "directory.open": "Enter",
    "file.preview": "Enter",
    "selection.toggle": "Space",
    "selection.all": "A",
    "selection.clearAll": "U",
    "file.copy": "C",
    "file.move": "M",
    "file.delete": "D",
    "file.rename": "R"
  }
}
```

The exact default keys for commands other than arrows and Enter are still open,
but command IDs and loading behavior should be implemented early.

### 8.2 Required Default Navigation Keys

The default KeyMap must include:

- `Up` for `cursor.up`.
- `Down` for `cursor.down`.
- `Left` for `cursor.left`.
- `Right` for `cursor.right`.
- `Enter` for opening the current item.

`Enter` is context-sensitive:

- If the current item is a directory, execute `directory.open`.
- If the current item is a file, execute `file.preview`.

The command resolution model should allow this behavior to become customizable
later. For MVP, it may be implemented as a context-aware command binding instead
of exposing a full behavior editor.

### 8.3 Default KeyMap

WsFiler's default KeyMap is original to the application. It should favor direct,
single-key operation for common file actions while keeping destructive actions
behind confirmation dialogs.

Default bindings:

| Command ID | Default Key | Behavior |
| --- | --- | --- |
| `cursor.up` | `Up` | Move cursor up. |
| `cursor.down` | `Down` | Move cursor down. |
| `cursor.left` | `Left` | Pane-aware left action. |
| `cursor.right` | `Right` | Pane-aware right action. |
| `cursor.pageUp` | `PageUp` | Move cursor one page up. |
| `cursor.pageDown` | `PageDown` | Move cursor one page down. |
| `cursor.first` | `Home` | Move cursor to first item. |
| `cursor.last` | `End` | Move cursor to last item. |
| `directory.open` | `Enter` | Open directory when current item is a directory. |
| `file.preview` | `Enter` | Preview current file when a preview is available. |
| `directory.parent` | `Backspace` | Move to parent directory. |
| `pane.switch` | `Tab` | Switch active pane. |
| `selection.toggle` | `Space` | Toggle current item selection, then move cursor down. |
| `selection.all` | `A` | Select all items in the active pane. |
| `selection.clearAll` | `U` | Clear all selected items in the active pane. |
| `selection.clear` | `Escape` | Clear current selection when no dialog is open. |
| `file.copy` | `C` | Copy source items to inactive pane directory. |
| `file.move` | `M` | Move source items to inactive pane directory. |
| `file.delete` | `D` | Delete source items after confirmation. |
| `file.rename` | `R` | Rename current item. |
| `dialog.confirm` | `Enter` | Confirm the active dialog. |
| `dialog.cancel` | `Escape` | Cancel the active dialog. |

Dialog bindings are scoped to dialogs and do not conflict with the main file pane
bindings.

`Escape` is context-sensitive:

- If a dialog or preview is open, execute `dialog.cancel`.
- If no dialog or preview is open, execute `selection.clear`.

Future versions may allow command behavior customization in addition to key
customization. For MVP, only key assignment customization is required.

### 8.4 Selection Commands

Selection behavior for MVP:

- `selection.toggle` toggles the current item and then moves the cursor to the
  next item when possible.
- `selection.all` selects all selectable items in the active pane.
- `selection.clearAll` clears all selected items in the active pane.
- `selection.clear` clears the current selection when no dialog or preview is open.
- Directory navigation always clears selection in the pane being navigated.
- Range selection is out of scope for MVP.

### 8.5 Conflict Handling

When loading KeyMap settings:

- Parse all gestures.
- Detect duplicate gestures assigned to different commands.
- Reject invalid gestures.
- Fall back to defaults for invalid entries.
- Surface conflicts in the status area or log.
- Allow explicitly modeled context-sensitive bindings, such as `Enter` resolving
  to `directory.open` for directories and `file.preview` for files.

### 8.6 Platform Modifiers

The internal model should use logical modifiers:

- `Control`
- `Shift`
- `Alt`
- `Meta`

Avalonia input should be translated into this model at the app boundary.

## 9. Navigation Behavior

### 9.1 Pane-Aware Left and Right

`cursor.left` and `cursor.right` are pane-aware commands.

When the left pane is active:

- `cursor.left` navigates to the parent directory.
- `cursor.right` switches the active pane to the right pane.

When the right pane is active:

- `cursor.right` navigates to the parent directory.
- `cursor.left` switches the active pane to the left pane.

This makes the outward arrow move up one directory and the inward arrow move to
the other pane.

### 9.2 Directory Open and File Preview

When the cursor is on a directory:

1. Set pane loading state.
2. List target directory.
3. Replace pane item list.
4. Reset cursor to the first item.
5. Clear selection.
6. Update current path.

When the cursor is on a text file and text preview is available:

1. Open the text preview modal.
2. Keep pane state unchanged.

The default preview behavior should be command-driven through `file.preview`.
Future versions should allow users to customize whether opening a file previews
it, launches it with the platform default app, or runs another action.

Text preview is optional for MVP, but desirable. If implemented, it should be
limited to a lightweight read-only text preview.

When the cursor is on a non-text file or a file that cannot be previewed:

1. Leave pane state unchanged.
2. Show a concise status message that preview is not available.

### 9.3 Parent Directory

When a parent exists:

1. Navigate to the parent.
2. Try to place the cursor on the directory that was just left.

At a root path, the command should leave the pane unchanged and show a status
message.

## 10. File Operation Flows

### 10.1 Source Selection

File commands operate on:

- Selected items, if any exist.
- Otherwise the current cursor item.

### 10.2 Copy

1. Resolve source items from active pane.
2. Resolve destination directory from inactive pane.
3. Ask for conflict handling when the destination already contains an item with
   the same name.
4. Execute copy asynchronously.
5. Refresh source and destination panes as needed.
6. Show success or failure status.

### 10.3 Move

1. Resolve source items from active pane.
2. Resolve destination directory from inactive pane.
3. Ask for conflict handling when the destination already contains an item with
   the same name.
4. Execute move asynchronously.
5. Refresh both panes.
6. Preserve cursor position where possible.

### 10.4 Destination Conflict Handling

When copy or move encounters a destination item with the same name, MVP behavior
must show a confirmation dialog with:

- Overwrite.
- Skip.
- Cancel.
- Apply to all remaining files checkbox.

The decision applies only to the currently running copy or move operation.

Behavior:

- Overwrite replaces the destination item.
- Skip leaves the destination item unchanged and continues with the next source
  item.
- Cancel stops the whole operation.
- Apply to all remaining files reuses the selected overwrite or skip decision for
  subsequent conflicts in the same operation.

The conflict result should be represented as a structured value, not as UI text.
Suggested values:

- `Overwrite`
- `Skip`
- `Cancel`

The "apply to all" flag should be tracked separately from the selected action.

### 10.5 Delete

1. Resolve target items from active pane.
2. Ask for delete confirmation.
3. Execute permanent delete asynchronously.
4. Refresh active pane.
5. Move cursor to a nearby item.

MVP delete behavior is permanent delete.

Recycle bin/trash support is out of scope for MVP. The delete implementation
should still be behind a service boundary so a platform-specific recycle
bin/trash delete mode can be added later without changing command handlers.

The delete confirmation message must clearly show:

- The number of selected target items.
- The current item name when deleting one item.
- One representative item name when deleting multiple items.
- That deletion is permanent.

### 10.6 Rename

1. Resolve current cursor item.
2. Ask for the new name.
3. Validate the name.
4. Execute rename.
5. Refresh active pane.
6. Move cursor to the renamed item.

## 11. Dialog Design

Dialogs required by MVP:

- Confirmation dialog.
- Rename input dialog.
- Optional text preview modal.
- Error dialog or inline error surface.

Dialogs must be keyboard operable:

- Confirm by command ID `dialog.confirm`.
- Cancel by command ID `dialog.cancel`.
- Initial focus should be predictable.
- Escape should cancel.

Presentation should depend on `IUserInteractionService`, not direct window calls
inside application services.

### 11.1 Text Preview Modal

The text preview modal should:

- Open from `file.preview` when the current file is previewable text.
- Display read-only text content.
- Close immediately with `Escape`.
- Close through `dialog.cancel`.
- Keep the active pane and cursor unchanged after closing.
- Display up to the first 100 KB of the file.
- Indicate when the file is larger and only the beginning is shown.

Previewing binary files, images, PDFs, archives, and rich document formats is out
of scope for MVP.

## 12. Error Handling

File system failures should become structured application errors.

Examples:

- Path not found.
- Access denied.
- Destination already exists.
- Invalid file name.
- File in use.
- Network path unavailable.
- Unknown IO error.

User-facing messages should be concise. Detailed exception data should go to logs.

## 13. Logging

MVP logging should capture:

- Application startup.
- Settings load failures.
- KeyMap parse failures.
- File operation failures.
- Unexpected exceptions.

MVP logging must use Microsoft.Extensions.Logging. Core code should depend only on
logging abstractions where logging is needed.

## 14. Localization Design

WsFiler must support Japanese and English UI text from MVP.

### 14.1 Resource Model

User-facing strings should be referenced by stable resource keys instead of being
hard-coded in views or view models.

Required languages:

- Japanese
- English

Resource files must use `.resx` so they can be edited easily in Visual Studio
2026.

Suggested resource files:

```text
src/
  WsFiler.Presentation/
    Resources/
      Strings.resx
      Strings.ja.resx
```

`Strings.resx` is the neutral English resource and fallback. English must not use
a separate `Strings.en.resx` file. Japanese strings should be stored in
`Strings.ja.resx`.

Resource keys should be language-neutral and stable, for example:

- `Command.Copy`
- `Command.Move`
- `Dialog.Overwrite.Title`
- `Dialog.Overwrite.Overwrite`
- `Dialog.Overwrite.Skip`
- `Dialog.Overwrite.Cancel`
- `Status.PreviewNotAvailable`

### 14.2 Language Selection

Initial language should be selected from the OS/user culture.

When settings persistence is available, the selected language should be stored in
user settings. Supported values should include:

- `auto`
- `ja`
- `en`

### 14.3 Scope

Must be localized:

- Menus.
- Command hints.
- Dialog titles and buttons.
- Status messages.
- Error messages shown to the user.
- Preview modal UI text.

Does not need localization:

- Stable command IDs.
- Log event IDs.
- Developer-facing exception details.

## 15. UI Component Design

### 15.1 MainWindow

Main window layout:

```text
+--------------------------------------------------------------+
| Top command/status strip                                     |
+------------------------------+-------------------------------+
| Left file pane               | Right file pane               |
| Path                         | Path                          |
| File list                    | File list                     |
| Pane summary                 | Pane summary                  |
+------------------------------+-------------------------------+
| Bottom command hints and operation status                    |
+--------------------------------------------------------------+
```

### 15.2 FilePane

The file pane should be a reusable Avalonia control.

Responsibilities:

- Render path.
- Render file list.
- Render active/inactive state.
- Render cursor and selection state.
- Forward keyboard focus to the main command pipeline.

The MVP file list should use Avalonia `DataGrid`. A custom file list control may
be considered later if `DataGrid` cannot meet density, keyboard handling, or
performance requirements.

### 15.3 File List Columns

Initial columns:

- Name
- Extension
- Size
- Modified

Item type should be visually identifiable without requiring a wide attribute
column. The MVP should show type through a compact leading symbol, icon, or
short marker in the name area.

Types that must be distinguishable:

- Directory
- File
- Symbolic link
- Other file system item

Selection state should be shown primarily through row color and focus styling,
not through a dedicated selection-mark column.

Out of scope for MVP:

- Attributes
- Owner

### 15.4 File List Sorting

Initial sort:

- Name ascending.
- Directories always before files.
- Case-insensitive comparison.

The sorting model should still store sort column and sort direction in pane state
so later versions can add sorting by extension, size, and modified date.

## 16. Styling Direction

The MVP theme should use:

- Dense rows.
- Clear typography.
- Subtle separators.
- Modern focus outline.
- Distinct active pane accent.
- Distinct selected-row treatment.

Theme modes:

- Light.
- Dark.
- Follow OS setting.

Avoid:

- Large cards.
- Marketing-style hero layouts.
- Excessive decorative gradients.
- Low-density spacing.

## 17. Settings and Session Design

Settings must be persisted in `settings.json`.

MVP settings should include:

- Language: `auto`, `ja`, or `en`.
- Theme: `system`, `light`, or `dark`.
- Restore session: boolean.
- Last left pane path.
- Last right pane path.
- KeyMap overrides.

Startup directory behavior:

- If session restore is enabled and previous paths are valid, restore both panes
  to the previous left and right directories.
- If no previous session is available, or a previous path is no longer available,
  use the user's home directory.
- On first launch, both panes open at the user's home directory.

## 18. Testing Strategy

MVP tests should use xUnit.

### 18.1 Core Tests

Test:

- Path value behavior.
- Selection behavior.
- Command ID definitions.
- Key gesture parsing.

### 18.2 Core Application Behavior Tests

Test:

- Pane switching.
- Cursor movement.
- Directory navigation.
- Source item resolution for operations.
- KeyMap conflict detection.

### 18.3 Infra Tests

Test:

- Local directory listing using temporary directories.
- Copy, move, delete, and rename behavior using temporary directories.
- Settings load and save.
- Localization resource lookup and fallback.

Infra tests should avoid destructive operations outside temporary test
directories.

## 19. Implementation Order

Recommended MVP implementation order:

1. Create solution and project structure.
2. Add Core models and interfaces.
3. Add KeyMap model and parser.
4. Add pane state and command dispatcher.
5. Add local file system provider.
6. Add Avalonia shell with two DataGrid-based panes.
7. Connect keyboard input to command dispatcher.
8. Implement navigation.
9. Implement copy, move, delete, and rename.
10. Add settings persistence for KeyMap overrides.
11. Add Japanese and English localization resources.
12. Add theme mode support.
13. Add session restore.
14. Add focused xUnit tests.

## 20. Open Decisions

- Whether recycle bin/trash delete should be added after MVP.
- Whether additional non-MVP file list columns are needed.
- Exact initial light and dark theme colors.
- Whether .NET 10 and Avalonia 12 are viable for implementation.
