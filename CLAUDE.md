# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build src/WsFiler.slnx

# Run
dotnet run --project src/WsFiler.App/WsFiler.App.csproj

# Test all
dotnet test src/WsFiler.slnx

# Test single project
dotnet test tests/WsFiler.Core.Tests/WsFiler.Core.Tests.csproj
dotnet test tests/WsFiler.Infra.Tests/WsFiler.Infra.Tests.csproj
```

## Architecture

WsFiler is a cross-platform dual-pane file manager (Norton Commander style), keyboard-first, built with Avalonia 12 / .NET 10. It uses strict 4-layer clean architecture:

```
WsFiler.App        → Avalonia UI shell (AXAML views, dialogs, entry point)
WsFiler.Presentation → ViewModels (MVVM via CommunityToolkit.Mvvm)
WsFiler.Core       → Domain: commands, keymap, file models, pane state — NO Avalonia deps
WsFiler.Infra      → LocalFileSystemProvider, settings persistence, logging
```

Dependencies flow strictly downward: App → Presentation → Core ← Infra.

### Key Concepts

**Commands** (`WsFiler.Core/Commands/ApplicationCommandId.cs`): All user actions are string constants (e.g. `"navigate.up"`, `"file.copy"`). 24 MVP commands defined.

**KeyMap** (`WsFiler.Core/KeyMap/`): `KeyGesture` + `KeyBinding` map gestures to command IDs. `DefaultKeyMap` provides default bindings. Fully decoupled from Avalonia input types.

**FilePaneState** (`WsFiler.Core/Panes/`): Immutable record holding a pane's current path, items, cursor, selection, and sort state. Never mutated in-place.

**FileSystemItem** (`WsFiler.Core/Files/`): Immutable value object for directory entries. `IFileSystemProvider` abstracts all I/O.

**ViewModels** (`WsFiler.Presentation/ViewModels/`): `MainWindowViewModel` owns left/right `FilePaneViewModel` instances and coordinates file operations. `FilePaneViewModel` reflects `FilePaneState` and drives the DataGrid.

**Dialogs** (`WsFiler.App/Views/`): Separate AXAML dialogs for delete, copy, move, rename, conflict resolution, and text preview. Dialog coordination lives in `MainWindowViewModel`.

### Design Docs

`docs/requirements.md` and `docs/basic-design.md` are the authoritative specs. Consult them before adding features or changing behavior — they define command IDs, default key bindings, file operation flows, conflict handling, and localization requirements.

### Localization

String resources live in `WsFiler.Presentation/Resources/Strings.resx` (English) and `Strings.ja.resx` (Japanese). All user-visible strings must go through the resource files.
