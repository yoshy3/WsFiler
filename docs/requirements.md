# WsFiler Requirements

## 1. Overview

WsFiler is a cross-platform dual-pane file manager built with C#, .NET, and Avalonia.
It targets Windows, macOS, and Linux, and is designed for users who want fast,
dense, keyboard-oriented file operations.

The initial target runtime and UI framework are .NET 10 and Avalonia 12. If
implementation or package compatibility issues are found, an older stable version
may be considered.

The application uses a classic two-pane filer layout while adopting a modern visual
style for colors, spacing, focus indicators, and control shapes.

## 2. Product Goals

- Provide a fast dual-pane file management workflow.
- Allow most daily operations to be completed using only the keyboard.
- Support a fully custom default key operation model.
- Allow users to customize the KeyMap.
- Run on Windows, macOS, and Linux using a shared codebase.
- Support local file systems and Windows UNC paths.
- Keep the first release focused on core file management behavior.

## 3. Target Platforms

- Windows
- macOS
- Linux

## 4. File System Scope

### In Scope

- Local files and directories.
- Windows drive paths such as `C:\Users`.
- Windows UNC paths such as `\\server\share\folder`.
- macOS local paths.
- Linux local paths.
- Hidden files and directories.
- Read-only files.
- Permission errors.
- Symbolic links.

### Out of Scope for MVP

- FTP.
- SFTP.
- Built-in SMB browsing beyond paths exposed through the operating system.
- Cloud storage provider integration.
- Archive browsing.
- Virtual file systems.

## 5. MVP Scope

The MVP includes:

- Dual-pane file list display.
- Keyboard-driven navigation.
- Directory movement.
- File and directory copy.
- File and directory move.
- Copy and move conflict confirmation.
- File and directory delete.
- File and directory rename.
- Optional basic text preview when opening a text file.
- File list sorting.
- Japanese and English UI text.
- Session restore.
- Light, dark, and OS-following themes.
- KeyMap foundation.

The MVP does not include:

- Full settings UI.
- Search UI.
- Advanced filters.
- Bulk rename.
- Advanced file preview.
- Non-text file preview.
- Text viewer.
- Binary viewer.
- Archive operations.
- Plugin system.

## 6. User Interface Requirements

### 6.1 Layout

The main window consists of:

- A compact top area for application commands and current state.
- Two side-by-side file panes.
- A compact bottom area for command hints, operation status, and messages.

Each file pane shows:

- Current path.
- File and directory list.
- Current cursor position.
- Selection state.
- Basic file metadata.

### 6.2 Visual Direction

- Classic high-density filer layout.
- Modern colors and control styling.
- Compact spacing.
- Clear active-pane indication.
- Clear keyboard focus indication.
- Clear current-row indication.
- Clear selected-item indication.

The UI should feel efficient and information-dense, not like a landing page or
document browser.

## 7. Keyboard Operation Requirements

Keyboard operation is a primary product requirement.

The MVP must support keyboard-only operation for:

- Switching the active pane.
- Moving the cursor.
- Moving left or right with pane-aware behavior.
- Opening a directory.
- Previewing a file.
- Moving to the parent directory.
- Selecting and unselecting items.
- Selecting all items.
- Clearing all selected items.
- Copying selected items.
- Moving selected items.
- Deleting selected items.
- Renaming the current item.
- Confirming or cancelling dialogs.

Mouse operation may exist, but it is secondary.

## 8. KeyMap Requirements

WsFiler uses an original default key operation model. It should not copy an existing
file manager's keymap as its default behavior.

The application must provide a KeyMap foundation with:

- Stable command identifiers.
- Default key bindings.
- User-overridable key bindings.
- Conflict detection.
- Platform-aware modifier handling.
- A structure that can later support keymap presets.

MVP implementation may use a configuration file before a full settings UI exists.

### 8.1 MVP Commands

The MVP KeyMap must cover at least:

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

## 9. File Operation Requirements

### 9.1 Copy

- Copy selected files and directories from the active pane to the inactive pane.
- Confirm when the destination already contains an item with the same name.
- The confirmation must support overwrite, skip, cancel, and applying the choice
  to all remaining files in the same operation.
- Report failures without crashing the application.

### 9.2 Move

- Move selected files and directories from the active pane to the inactive pane.
- Confirm when the destination already contains an item with the same name.
- The confirmation must support overwrite, skip, cancel, and applying the choice
  to all remaining files in the same operation.
- Report failures without crashing the application.

### 9.3 Delete

- Delete selected files and directories.
- Require confirmation before deleting.
- Report failures without crashing the application.

### 9.4 Rename

- Rename the current item in the active pane.
- Reject invalid names.
- Report failures without crashing the application.

## 10. Non-Functional Requirements

- The UI must remain responsive during file operations.
- File operations should run asynchronously.
- Long-running operations should expose progress when feasible.
- Destructive operations must require confirmation.
- Errors must be visible and understandable to the user.
- Settings must be persisted per user.
- UI text must support Japanese and English.
- User settings must be stored in `settings.json`.
- Logging must use Microsoft.Extensions.Logging.
- The design should be testable without depending directly on Avalonia controls.
- OS-specific behavior should be isolated where possible.

## 11. Localization Requirements

WsFiler must support Japanese and English UI text.

MVP requirements:

- All user-facing UI text must be localizable.
- Japanese and English `.resx` resources must be provided.
- Localization resources must be editable with Visual Studio 2026.
- The application should choose the initial language from the OS/user culture.
- A persisted language setting should be supported when settings infrastructure is
  available.
- Log messages and internal command IDs do not need to be localized.
- KeyMap command IDs must remain stable, language-neutral identifiers.

## 12. Architecture Direction

The initial architecture should separate:

- UI layer: Avalonia views and controls.
- ViewModel layer: pane state, selection state, command state.
- Core layer: domain models, commands, KeyMap, file operation orchestration.
- File system layer: local and UNC-aware file system access.
- Infra layer: settings persistence, logging, OS-specific services.

The file system layer should be shaped so future providers can be added without
rewriting the UI.

## 13. Open Decisions

- Whether additional non-MVP commands need default keys.
- Whether additional non-MVP file list columns are needed.
- Whether recycle bin/trash delete should be added after MVP.
- Initial theme colors.
