#!/usr/bin/env bash
set -euo pipefail

VERSION="$1"
SOURCE_DIR="$2"
DEST_DIR="$3"

PACKAGE_NAME="wsfiler"
ARCH="amd64"
INSTALL_PREFIX="/usr"
DEB_ROOT="$(mktemp -d)"

echo "Building ${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
echo "  Source : $SOURCE_DIR"
echo "  Output : $DEST_DIR"

# ── Directory structure ──────────────────────────────────────────────────
mkdir -p "$DEB_ROOT/DEBIAN"
mkdir -p "$DEB_ROOT${INSTALL_PREFIX}/bin"
mkdir -p "$DEB_ROOT${INSTALL_PREFIX}/share/wsfiler"
mkdir -p "$DEB_ROOT${INSTALL_PREFIX}/share/applications"

# ── Copy binary and native libs ──────────────────────────────────────────
cp "$SOURCE_DIR/WsFiler.App" "$DEB_ROOT${INSTALL_PREFIX}/share/wsfiler/WsFiler.App"
chmod 755 "$DEB_ROOT${INSTALL_PREFIX}/share/wsfiler/WsFiler.App"

find "$SOURCE_DIR" -maxdepth 1 -name "*.so" -exec cp {} "$DEB_ROOT${INSTALL_PREFIX}/share/wsfiler/" \;

if [ -d "$SOURCE_DIR/ja" ]; then
  mkdir -p "$DEB_ROOT${INSTALL_PREFIX}/share/wsfiler/ja"
  cp -r "$SOURCE_DIR/ja/"* "$DEB_ROOT${INSTALL_PREFIX}/share/wsfiler/ja/"
fi

# ── Launcher symlink ─────────────────────────────────────────────────────
ln -s "${INSTALL_PREFIX}/share/wsfiler/WsFiler.App" \
      "$DEB_ROOT${INSTALL_PREFIX}/bin/wsfiler"

# ── .desktop file ────────────────────────────────────────────────────────
cat > "$DEB_ROOT${INSTALL_PREFIX}/share/applications/wsfiler.desktop" <<DESKTOP
[Desktop Entry]
Version=1.0
Type=Application
Name=WsFiler
Comment=Cross-platform dual-pane file manager
Exec=${INSTALL_PREFIX}/bin/wsfiler
Icon=wsfiler
Categories=Utility;FileManager;
Keywords=files;file manager;dual pane;
StartupNotify=true
DESKTOP

# ── DEBIAN/control ───────────────────────────────────────────────────────
INSTALLED_SIZE=$(du -sk "$SOURCE_DIR" | cut -f1)

cat > "$DEB_ROOT/DEBIAN/control" <<CONTROL
Package: ${PACKAGE_NAME}
Version: ${VERSION}
Architecture: ${ARCH}
Maintainer: yoshy3 <yoshy3@gmail.com>
Installed-Size: ${INSTALLED_SIZE}
Depends: libx11-6, libfontconfig1, libglib2.0-0
Description: WsFiler - cross-platform dual-pane file manager
 A keyboard-first dual-pane file manager built with .NET 10 and Avalonia.
 Supports copy, move, delete, rename with conflict resolution.
 Japanese and English UI, light/dark/system themes.
Homepage: https://github.com/yoshy3/WsFiler
CONTROL

# ── DEBIAN/postinst ──────────────────────────────────────────────────────
cat > "$DEB_ROOT/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database -q /usr/share/applications
fi
POSTINST
chmod 755 "$DEB_ROOT/DEBIAN/postinst"

# ── Build package ────────────────────────────────────────────────────────
mkdir -p "$DEST_DIR"
dpkg-deb --build --root-owner-group "$DEB_ROOT" \
  "${DEST_DIR}/${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"

echo "Done: ${DEST_DIR}/${PACKAGE_NAME}_${VERSION}_${ARCH}.deb"
rm -rf "$DEB_ROOT"
