# WsFiler

A cross-platform dual-pane file manager built with C#, .NET 10, and Avalonia. Designed for fast, keyboard-driven file operations.

[日本語](README_ja.md)

## Features

- Dual-pane layout for efficient file management
- Keyboard-first operation — most daily tasks require no mouse
- Copy, move, delete, and rename with conflict resolution
- Text file preview
- File list sorting
- Japanese and English UI
- Session restore
- Light, dark, and OS-following themes
- Customizable key bindings

## Platform Support

Windows, macOS, Linux

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Build & Run

```bash
# Build
dotnet build src/WsFiler.slnx

# Run
dotnet run --project src/WsFiler.App/WsFiler.App.csproj

# Test
dotnet test src/WsFiler.slnx
```

## NativeAOT Build

### Windows x64

Before running, ensure `vswhere.exe` is in your PATH (typically `C:\Program Files (x86)\Microsoft Visual Studio\Installer`), or run from a Visual Studio Developer Command Prompt.

```bash
dotnet publish src/WsFiler.App/WsFiler.App.csproj \
  -r win-x64 -c Release \
  -p:PublishAot=true --self-contained true
```

Output is placed in `src/WsFiler.App/bin/Release/net10.0/win-x64/publish/`.

### macOS (arm64)

```bash
dotnet publish src/WsFiler.App/WsFiler.App.csproj \
  -r osx-arm64 -c Release \
  -p:PublishAot=true --self-contained true
```

> **Note:** The distributed `.dmg` is not code-signed. macOS Gatekeeper will block the app on first launch. To allow it, run once in Terminal:
>
> ```bash
> xattr -cr /Applications/WsFiler.app
> ```

### Linux (x64)

```bash
dotnet publish src/WsFiler.App/WsFiler.App.csproj \
  -r linux-x64 -c Release \
  -p:PublishAot=true --self-contained true
```

## Default Key Bindings

| Key | Command |
|-----|---------|
| `↑` / `↓` | Move cursor |
| `←` / `→` | Pane-aware left/right |
| `PageUp` / `PageDown` | Page up/down |
| `Home` / `End` | First / last item |
| `Enter` | Open directory or preview file |
| `Backspace` | Go to parent directory |
| `Tab` | Switch active pane |
| `Space` | Toggle selection |
| `A` | Select all |
| `U` | Clear all selections |
| `Escape` | Cancel dialog / clear selection |
| `C` | Copy to inactive pane |
| `M` | Move to inactive pane |
| `D` | Delete (with confirmation) |
| `R` | Rename current item |

Key bindings can be customized via `settings.json`.

## Architecture

WsFiler uses a 4-layer clean architecture:

| Layer | Project | Responsibility |
|-------|---------|---------------|
| UI | `WsFiler.App` | Avalonia views, dialogs, entry point |
| ViewModel | `WsFiler.Presentation` | MVVM view models, dialog coordination |
| Domain | `WsFiler.Core` | Commands, key map, file models, pane state — no Avalonia dependency |
| Infrastructure | `WsFiler.Infra` | File system access, settings, logging |

See [`docs/basic-design.md`](docs/basic-design.md) for the full architecture specification.
