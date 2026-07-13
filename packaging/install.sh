#!/usr/bin/env bash
# Установка Vantah в пользовательский префикс (по умолчанию ~/.local): бинарь,
# иконки и .desktop, после чего Vantah появляется в меню приложений и запускается кликом.
#
#   packaging/install.sh              # собрать и установить
#   packaging/install.sh --uninstall  # удалить всё установленное
#   PREFIX=/usr/local packaging/install.sh   # системная установка (нужен root)
#
# Root не требуется: всё кладётся в домашний каталог по XDG-путям.
set -euo pipefail

PREFIX=${PREFIX:-$HOME/.local}
BIN="$PREFIX/bin/vantah"
LIBDIR="$PREFIX/lib/vantah"
APPS="$PREFIX/share/applications"
ICONS="$PREFIX/share/icons/hicolor"

here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/.." && pwd)

refresh_caches() {
  # Без этого GNOME/KDE не увидят новый пункт меню до перелогина.
  command -v update-desktop-database >/dev/null && update-desktop-database "$APPS" 2>/dev/null || true
  command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -qtf "$ICONS" 2>/dev/null || true
}

if [ "${1:-}" = "--uninstall" ]; then
  rm -f "$BIN" "$APPS/vantah.desktop"
  rm -rf "$LIBDIR"
  find "$ICONS" -name 'vantah.png' -delete 2>/dev/null || true
  refresh_caches
  echo "Vantah удалён из $PREFIX"
  exit 0
fi

# --- бинарь: self-contained single-file, чтобы .desktop не зависел от dotnet в PATH
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
echo "==> Публикация (dotnet publish -c Release -r linux-x64)…"
dotnet publish "$root/src/Vantah.App" -c Release -r linux-x64 -o "$tmp/pub" --nologo -v quiet

install -Dm755 "$tmp/pub/Vantah.App" "$LIBDIR/vantah"
mkdir -p "$(dirname "$BIN")"
ln -sfn "$LIBDIR/vantah" "$BIN"

# --- иконки приложения (те же PNG, что и в репозитории)
for png in "$root"/packaging/icons/hicolor/*/apps/vantah.png; do
  size=$(basename "$(dirname "$(dirname "$png")")")
  install -Dm644 "$png" "$ICONS/$size/apps/vantah.png"
done

# --- .desktop: Exec подменяем на абсолютный путь — PATH сессии не обязан содержать ~/.local/bin
mkdir -p "$APPS"
sed "s|^Exec=vantah$|Exec=$BIN|" "$here/vantah.desktop" > "$APPS/vantah.desktop"
chmod 644 "$APPS/vantah.desktop"

refresh_caches

command -v desktop-file-validate >/dev/null && desktop-file-validate "$APPS/vantah.desktop"

echo "OK: $BIN, $APPS/vantah.desktop, иконки в $ICONS"
echo "Vantah должен появиться в меню приложений (может понадобиться пара секунд)."
