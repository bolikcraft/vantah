# AUR: пакет `vantah-bin`

`PKGBUILD` рядом ставит готовый бинарь из GitHub-релиза, не собирая проект из
исходников — отсюда суффикс `-bin`. Имя `vantah` в AUR на момент написания свободно,
конфликтов нет.

Поддерживаются две архитектуры, ассеты берутся разные:

| `$CARCH` | ассет релиза | каталог внутри архива |
|----------|--------------|-----------------------|
| `x86_64` | `vantah-<ver>-linux-x64.tar.gz` | `vantah-<ver>-linux-x64` |
| `aarch64` | `vantah-<ver>-linux-arm64.tar.gz` | `vantah-<ver>-linux-arm64` |

Имена каталогов внутри архивов идут от .NET RID и не совпадают с `$CARCH`, поэтому
`package()` выбирает каталог по `case "$CARCH"`, а источники расписаны как
`source_x86_64` / `source_aarch64`.

Архив релиза должен содержать `share/applications`, `share/metainfo` и `share/icons` —
это добавлено в `.github/workflows/release.yml` и появится начиная с **v0.3.4**. На более
ранних релизах `package()` упадёт: там внутри архива только бинарь, README и LICENSE.

arm64-ассеты выкладываются начиная с **v0.3.5**; суммы обеих архитектур подставляет
`updpkgsums`.

## Первая публикация

Нужен аккаунт на https://aur.archlinux.org с добавленным SSH-ключом
(Мой профиль → SSH Public Key).

```bash
git clone ssh://aur@aur.archlinux.org/vantah-bin.git
cd vantah-bin
cp /path/to/vantah/packaging/aur/PKGBUILD .

# Реальная контрольная сумма вместо SKIP (пакет updpkgsums из pacman-contrib):
updpkgsums

# .SRCINFO обязателен, без него AUR отклонит push:
makepkg --printsrcinfo > .SRCINFO

# Проверка, что пакет вообще собирается и ставится:
makepkg -si

git add PKGBUILD .SRCINFO
git commit -m "Initial import: vantah-bin 0.3.4"
git push
```

Всё это делается на машине с Arch (или в arch-контейнере): `makepkg`, `updpkgsums` и
`namcap` в других дистрибутивах отсутствуют.

## Обновление на новый релиз

```bash
cd vantah-bin
sed -i "s/^pkgver=.*/pkgver=0.3.6/; s/^pkgrel=.*/pkgrel=1/" PKGBUILD
updpkgsums
makepkg --printsrcinfo > .SRCINFO
git commit -am "Update to 0.3.6"
git push
```

`pkgrel` увеличивается только когда меняется сам `PKGBUILD` без смены версии.

## Что стоит проверить перед первым push

- `makepkg -si` прогоняется на x86_64; сборку под `aarch64` проверить на реальной
  arm64-машине (или `makepkg --config` в arm64-контейнере) — эмуляция для этого не нужна,
  `package()` только распаковывает и раскладывает файлы.
- `namcap PKGBUILD` и `namcap vantah-bin-*.pkg.tar.zst` — ловит лишние и недостающие
  зависимости. Список `depends` собран по тому, что тянут self-contained .NET и Avalonia
  (X11, ICU, OpenSSL, fontconfig); на живой системе Arch он не проверялся.
- Запуск после установки: `vantah` из PATH и пункт в меню приложений.
- `adguardvpn-cli` в `optdepends`, а не в `depends`, намеренно: её часто ставят
  официальным установщиком AdGuard мимо pacman, и жёсткая зависимость мешала бы.
