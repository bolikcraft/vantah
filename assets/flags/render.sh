#!/usr/bin/env bash
# Флаги стран: SVG из flag-icons → PNG в ресурсах приложения. Запускается вручную
# (при обновлении набора), результат коммитится: вешать `dotnet build` на inkscape
# и на клонирование чужого репозитория нельзя — сборка перестанет быть воспроизводимой.
#
# Источник: https://github.com/lipis/flag-icons, лицензия MIT (см. LICENSE.flag-icons
# рядом с этим скриптом). Берётся вариант 4x3 — из него и получается 72×54.
#
#   ./render.sh                 # склонировать набор во временный каталог и отрендерить
#   ./render.sh ~/src/flag-icons  # использовать уже имеющуюся копию
set -euo pipefail

cd "$(dirname "$0")"
root=$(git rev-parse --show-toplevel)
out="$root/src/Vantah.App/Assets/flags"

WIDTH=72   # 4x3 → 72×54; размер строки списка локаций
HEIGHT=54

tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT

src="${1:-}"
if [[ -z "$src" ]]; then
  src="$tmp/flag-icons"
  git clone --depth 1 https://github.com/lipis/flag-icons.git "$src"
fi

svgdir="$src/flags/4x3"
[[ -d "$svgdir" ]] || { echo "ОШИБКА: нет каталога $svgdir" >&2; exit 1; }

mkdir -p "$out"
count=0
for svg in "$svgdir"/*.svg; do
  code=$(basename "$svg" .svg)
  # stderr не глушим: иначе падение inkscape оборвёт скрипт без единой диагностики.
  inkscape "$svg" --export-type=png --export-width="$WIDTH" --export-height="$HEIGHT" \
    -o "$out/$code.png" >/dev/null
  count=$((count + 1))
done

# Код страны из CLI ищется среди этих файлов по имени (LocationItemViewModel), поэтому
# набор не должен молча поредеть: пустой или подозрительно короткий результат — ошибка.
if (( count < 200 )); then
  echo "ОШИБКА: отрендерено всего $count флагов — набор неполный" >&2
  exit 1
fi

echo "OK: $count флагов в $out"
