#!/usr/bin/env bash
# SVG → ICO/PNG. Запускается вручную при правке знака, результат коммитится:
# вешать `dotnet build` на inkscape нельзя — сборка перестанет быть воспроизводимой.
set -euo pipefail

cd "$(dirname "$0")"
root=$(git rev-parse --show-toplevel)
assets="$root/src/Vantah.App/Assets"
hicolor="$root/packaging/icons/hicolor"
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

DARK='#18181b'   # знак для светлых панелей
LIGHT='#f4f4f5'  # знак для тёмных панелей

png() {  # png <svg> <size> <out>
  # stderr не глушим: иначе падение inkscape оборвёт скрипт без единой диагностики.
  inkscape "$1" --export-type=png --export-width="$2" --export-height="$2" -o "$3" >/dev/null
}

# --- иконка приложения: один ICO со всеми размерами + PNG для hicolor
app_sizes=(16 32 48 64 128 256)
app_pngs=()
for s in "${app_sizes[@]}"; do
  png vantah.svg "$s" "$tmp/app-$s.png"
  app_pngs+=("$tmp/app-$s.png")
  mkdir -p "$hicolor/${s}x${s}/apps"
  cp "$tmp/app-$s.png" "$hicolor/${s}x${s}/apps/vantah.png"
done
magick "${app_pngs[@]}" "$assets/vantah.ico"

# --- трей: 2 комплекта × 3 глифа. Светлый — тот же SVG с заменённым цветом.
tray_sizes=(16 22 24 32 48)
mkdir -p "$assets/tray"
for state in disconnected connecting connected; do
  for polarity in dark light; do
    src="tray-$state.svg"
    if [ "$polarity" = light ]; then
      src="$tmp/tray-$state-light.svg"
      sed "s/$DARK/$LIGHT/g" "tray-$state.svg" > "$src"
      # Замена цвета обязана сработать: иначе светлый комплект молча станет копией
      # тёмного и знак пропадёт на тёмной панели.
      if cmp -s "$src" "tray-$state.svg"; then
        echo "ОШИБКА: в tray-$state.svg не найден цвет $DARK — светлый комплект не собрать" >&2
        exit 1
      fi
    fi
    pngs=()
    for s in "${tray_sizes[@]}"; do
      png "$src" "$s" "$tmp/$polarity-$state-$s.png"
      pngs+=("$tmp/$polarity-$state-$s.png")
    done
    magick "${pngs[@]}" "$assets/tray/$polarity-$state.ico"
  done
done

echo "OK: $assets/vantah.ico + 6 иконок в $assets/tray"
