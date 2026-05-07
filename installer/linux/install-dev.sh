#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ICON_SOURCE_DIR="$REPO_ROOT/src/WsFiler.App/Assets/linux"

ICON_DEST_BASE="$HOME/.local/share/icons/hicolor"
DESKTOP_DIR="$HOME/.local/share/applications"

echo "Installing WsFiler development desktop entry..."

for size in 16 32 48 64 128 256 512; do
  src="$ICON_SOURCE_DIR/wsfiler-${size}.png"
  dest_dir="$ICON_DEST_BASE/${size}x${size}/apps"
  if [ -f "$src" ]; then
    mkdir -p "$dest_dir"
    cp "$src" "$dest_dir/wsfiler.png"
    echo "  icon ${size}x${size} -> $dest_dir/wsfiler.png"
  fi
done

mkdir -p "$DESKTOP_DIR"
cat > "$DESKTOP_DIR/wsfiler-dev.desktop" <<DESKTOP
[Desktop Entry]
Version=1.0
Type=Application
Name=WsFiler (dev)
Comment=Cross-platform dual-pane file manager
Exec=dotnet run --project $REPO_ROOT/src/WsFiler.App/WsFiler.App.csproj
Icon=wsfiler
Categories=Utility;FileManager;
Keywords=files;file manager;dual pane;
StartupNotify=true
StartupWMClass=WsFiler
DESKTOP

echo "  desktop -> $DESKTOP_DIR/wsfiler-dev.desktop"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database -q "$DESKTOP_DIR"
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -q "$ICON_DEST_BASE" || true
fi

echo "Done. Re-launch the app for the icon to appear."
