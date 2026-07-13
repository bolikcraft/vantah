#!/usr/bin/env bash
# SVG → ICO/PNG. Запускается вручную при правке знака, результат коммитится:
# вешать `dotnet build` на inkscape нельзя — сборка перестанет быть воспроизводимой.
set -euo pipefail

cd "$(dirname "$0")"
root=$(git rev-parse --show-toplevel)
assets="$root/src/Vantah.App/Assets"
hicolor="$root/packaging/icons/hicolor"
tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

# Цвет знака в исходниках трея — его и подменяем на цвет состояния.
MARK='#18181b'

# Цвет состояния трея. Дублирует форму знака, не заменяет её: см.
# docs/specs/2026-07-13-icon-design.md.
declare -A TRAY_COLOR=(
  [disconnected]='#9ca3af'  # серый — защиты нет (сюда же Error)
  [connecting]='#f59e0b'    # янтарный — переходное состояние
  [connected]='#22c55e'     # зелёный — маршрут построен
)

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

# --- трей: 3 глифа, каждый в цвете своего состояния.
tray_sizes=(16 22 24 32 48)
mkdir -p "$assets/tray"
for state in disconnected connecting connected; do
  color="${TRAY_COLOR[$state]}"
  src="$tmp/tray-$state.svg"
  sed "s/$MARK/$color/g" "tray-$state.svg" > "$src"
  # Замена цвета обязана сработать: молчаливый no-op в sed отправил бы в трей знак
  # цвета исходника (почти чёрный) — состояние перестало бы читаться цветом вовсе.
  if cmp -s "$src" "tray-$state.svg"; then
    echo "ОШИБКА: в tray-$state.svg не найден цвет $MARK — знак не покрасить в $color" >&2
    exit 1
  fi

  pngs=()
  for s in "${tray_sizes[@]}"; do
    png "$src" "$s" "$tmp/$state-$s.png"
    pngs+=("$tmp/$state-$s.png")
  done
  magick "${pngs[@]}" "$assets/tray/$state.ico"
done

echo "OK: $assets/vantah.ico + ${#TRAY_COLOR[@]} иконки в $assets/tray"
